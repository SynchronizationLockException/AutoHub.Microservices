using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
using RentalService.Api.Models;
using RentalService.Api.Services;
using System.Security.Claims;

namespace RentalService.Api.Endpoints;

public static class RentalEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void MapRentalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rentals", async (ClaimsPrincipal principal, int? page, int? pageSize, RentalDbContext db, CancellationToken ct) =>
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
            var query = db.Rentals.AsNoTracking();
            if (ownerFilter is not null)
            {
                query = query.Where(r => r.OwnerUsername == ownerFilter);
            }

            var skip = (currentPage - 1) * currentPageSize;
            var items = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/rentals/{id:guid}", async (Guid id, ClaimsPrincipal principal, RentalDbContext db, CancellationToken ct) =>
        {
            var rental = await db.Rentals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (rental is null)
            {
                return Results.NotFound();
            }

            if (!CanAccessByOwner(principal, rental.OwnerUsername))
            {
                return Results.Forbid();
            }

            return Results.Ok(rental);
        }).RequireAuthorization();

        app.MapPost("/api/rentals", CreateRentalWithIdempotencyAsync).RequireAuthorization();
    }

    private static async Task<IResult> CreateRentalWithIdempotencyAsync(
        HttpContext httpContext,
        CreateRentalRequest request,
        RentalSagaService sagaService,
        RentalDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        return await IdempotencyExecutionHelper.ExecuteAsync<RentalDbContext, IdempotentRequest>(
            httpContext,
            db,
            pathKey: "/api/rentals",
            createRecord: (keyHash, path) => new IdempotentRequest
            {
                KeyHash = keyHash,
                Path = path
            },
            action: () => CreateRentalInternalAsync(httpContext, request, sagaService, principal, ct),
            ct);
    }

    private static async Task<IResult> CreateRentalInternalAsync(
        HttpContext httpContext,
        CreateRentalRequest request,
        RentalSagaService sagaService,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!principal.IsInRole("Client") && !principal.IsInRole("Manager") && !principal.IsInRole("Admin"))
        {
            return Results.Forbid();
        }

        if (!TryResolveRentalOwnerUsername(principal, request, out var ownerUsername, out var ownerError))
        {
            return ownerError!;
        }

        var correlationId = httpContext.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        var (rental, error) = await sagaService.StartCreateRentalAsync(
            request,
            ownerUsername,
            correlationId,
            ct);
        if (error is not null)
        {
            return error;
        }

        return Results.Created($"/api/rentals/{rental!.Id}", rental);
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

    private static bool TryResolveRentalOwnerUsername(
        ClaimsPrincipal principal,
        CreateRentalRequest request,
        out string ownerUsername,
        out IResult? error)
    {
        error = null;
        if (principal.IsInRole("Client"))
        {
            var name = principal.Identity?.Name;
            if (string.IsNullOrEmpty(name))
            {
                ownerUsername = string.Empty;
                error = Results.Unauthorized();
                return false;
            }

            ownerUsername = name.ToLowerInvariant();
            return true;
        }

        var customerLogin = request.CustomerName.Trim();
        if (string.IsNullOrEmpty(customerLogin))
        {
            ownerUsername = string.Empty;
            error = Results.BadRequest("Customer name (account username) is required.");
            return false;
        }

        ownerUsername = customerLogin.ToLowerInvariant();
        return true;
    }
}
