using BuildingBlocks.Contracts;
using BuildingBlocks.Hosting.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentService.Api.Data;
using PaymentService.Api.Models;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace PaymentService.Api.Endpoints;

public static class PaymentEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/payments", async (ClaimsPrincipal principal, int? page, int? pageSize, PaymentDbContext db, CancellationToken ct) =>
        {
            if (!TryGetOwnerScope(principal, out var deny, out var ownerFilter))
            {
                return deny;
            }

            var currentPage = page.GetValueOrDefault(1);
            var currentPageSize = pageSize.GetValueOrDefault(DefaultPageSize);
            if (currentPage <= 0 || currentPageSize <= 0)
            {
                return Results.BadRequest("Query params page and pageSize must be positive integers.");
            }

            currentPageSize = Math.Min(currentPageSize, MaxPageSize);
            var query = db.Payments.AsNoTracking().AsQueryable();
            if (ownerFilter is not null)
            {
                query = query.Where(p => p.OwnerUsername == ownerFilter);
            }

            var skip = (currentPage - 1) * currentPageSize;
            var items = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/payments/{id:guid}", async (Guid id, ClaimsPrincipal principal, PaymentDbContext db, CancellationToken ct) =>
        {
            var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (payment is null)
            {
                return Results.NotFound();
            }

            if (!CanAccessByOwner(principal, payment.OwnerUsername))
            {
                return Results.Forbid();
            }

            return Results.Ok(payment);
        }).RequireAuthorization();

        app.MapPost("/api/payments", CreatePaymentWithIdempotencyAsync).RequireAuthorization();
    }

    private static async Task<IResult> CreatePaymentWithIdempotencyAsync(
        HttpContext httpContext,
        CreatePaymentRequest request,
        IHttpClientFactory factory,
        PaymentDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        return await IdempotencyExecutionHelper.ExecuteAsync<PaymentDbContext, IdempotentRequest>(
            httpContext,
            db,
            pathKey: "/api/payments",
            createRecord: (keyHash, path) => new IdempotentRequest
            {
                KeyHash = keyHash,
                Path = path
            },
            action: () => CreatePaymentInternalAsync(httpContext, request, factory, db, principal, ct),
            ct);
    }

    private static async Task<IResult> CreatePaymentInternalAsync(
        HttpContext httpContext,
        CreatePaymentRequest request,
        IHttpClientFactory factory,
        PaymentDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!principal.IsInRole("Manager") && !principal.IsInRole("Admin"))
        {
            return Results.Forbid();
        }

        decimal expectedAmount;
        string ownerUsername;
        switch (request.ReferenceKind)
        {
            case PaymentReferenceKind.Sale:
            {
                var sales = factory.CreateClient("sales");
                using var saleRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/sales/{request.ReferenceId}");
                CopyAuthorizationFromCaller(httpContext, saleRequest);
                var response = await sales.SendAsync(saleRequest, ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return Results.NotFound("Sale order not found.");
                }

                response.EnsureSuccessStatusCode();
                var sale = await response.Content.ReadFromJsonAsync<SaleAmountResponse>(ct);
                if (sale is null)
                {
                    return Results.NotFound("Sale order not found.");
                }

                expectedAmount = sale.Price;
                ownerUsername = sale.OwnerUsername;
                break;
            }
            case PaymentReferenceKind.Rental:
            {
                var rentals = factory.CreateClient("rentals");
                using var rentalRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/rentals/{request.ReferenceId}");
                CopyAuthorizationFromCaller(httpContext, rentalRequest);
                var response = await rentals.SendAsync(rentalRequest, ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return Results.NotFound("Rental contract not found.");
                }

                response.EnsureSuccessStatusCode();
                var rental = await response.Content.ReadFromJsonAsync<RentalAmountResponse>(ct);
                if (rental is null)
                {
                    return Results.NotFound("Rental contract not found.");
                }

                expectedAmount = rental.TotalPrice;
                ownerUsername = rental.OwnerUsername;
                break;
            }
            default:
                return Results.BadRequest("Unsupported reference kind.");
        }

        if (request.Amount != expectedAmount)
        {
            return Results.BadRequest("Amount does not match the referenced order total.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim();
        var completedAt = DateTime.UtcNow;
        var payment = new Payment
        {
            OwnerUsername = ownerUsername,
            ReferenceKind = request.ReferenceKind,
            ReferenceId = request.ReferenceId,
            Amount = request.Amount,
            Currency = currency,
            Status = PaymentStatus.Completed,
            CompletedAtUtc = completedAt
        };

        db.Payments.Add(payment);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "PaymentCompleted",
            Payload = JsonSerializer.Serialize(new PaymentCompletedEvent(
                payment.Id,
                payment.ReferenceKind.ToString(),
                payment.ReferenceId,
                payment.Amount,
                payment.Currency))
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict("A payment for this order already exists.");
        }

        return Results.Created($"/api/payments/{payment.Id}", payment);
    }

    private static void CopyAuthorizationFromCaller(HttpContext httpContext, HttpRequestMessage target)
    {
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        {
            target.Headers.TryAddWithoutValidation("Authorization", auth.ToString());
        }
    }

    private static bool TryGetOwnerScope(
        ClaimsPrincipal principal,
        out IResult? denyResult,
        out string? ownerUsernameFilter)
    {
        denyResult = null;
        ownerUsernameFilter = null;
        if (principal.IsInRole("Manager") || principal.IsInRole("Admin"))
        {
            return true;
        }

        if (principal.IsInRole("Client"))
        {
            var name = principal.Identity?.Name;
            if (string.IsNullOrEmpty(name))
            {
                denyResult = Results.Unauthorized();
                return false;
            }

            ownerUsernameFilter = name.ToLowerInvariant();
            return true;
        }

        denyResult = Results.Forbid();
        return false;
    }

    private static bool CanAccessByOwner(ClaimsPrincipal principal, string ownerUsername)
    {
        if (principal.IsInRole("Manager") || principal.IsInRole("Admin"))
        {
            return true;
        }

        var name = principal.Identity?.Name;
        return !string.IsNullOrEmpty(name) &&
               string.Equals(ownerUsername, name, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SaleAmountResponse(decimal Price, string OwnerUsername);

    private sealed record RentalAmountResponse(decimal TotalPrice, string OwnerUsername);
}
