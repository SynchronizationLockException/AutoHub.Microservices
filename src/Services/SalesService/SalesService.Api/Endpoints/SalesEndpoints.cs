using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using Microsoft.EntityFrameworkCore;
using SalesService.Api.Data;
using SalesService.Api.Models;
using SalesService.Api.Services;
using System.Security.Claims;

namespace SalesService.Api.Endpoints;

public static class SalesEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void MapSalesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sales", async (ClaimsPrincipal principal, int? page, int? pageSize, SalesDbContext db, CancellationToken ct) =>
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
            var query = db.Sales.AsNoTracking();
            if (ownerFilter is not null)
            {
                query = query.Where(s => s.OwnerUsername == ownerFilter);
            }

            var skip = (currentPage - 1) * currentPageSize;
            var items = await query
                .OrderByDescending(x => x.SoldAtUtc)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/sales/{id:guid}", async (Guid id, ClaimsPrincipal principal, SalesDbContext db, CancellationToken ct) =>
        {
            var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (sale is null)
            {
                return Results.NotFound();
            }

            if (!CanAccessByOwner(principal, sale.OwnerUsername))
            {
                return Results.Forbid();
            }

            return Results.Ok(sale);
        }).RequireAuthorization();

        app.MapPost("/api/sales", CreateSaleWithIdempotencyAsync).RequireAuthorization();
    }

    private static async Task<IResult> CreateSaleWithIdempotencyAsync(
        HttpContext httpContext,
        CreateSaleRequest request,
        SalesSagaService sagaService,
        SalesDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        return await IdempotencyExecutionHelper.ExecuteAsync<SalesDbContext, IdempotentRequest>(
            httpContext,
            db,
            pathKey: "/api/sales",
            createRecord: (keyHash, path) => new IdempotentRequest
            {
                KeyHash = keyHash,
                Path = path
            },
            action: () => CreateSaleInternalAsync(httpContext, request, sagaService, principal, ct),
            ct);
    }

    private static async Task<IResult> CreateSaleInternalAsync(
        HttpContext httpContext,
        CreateSaleRequest request,
        SalesSagaService sagaService,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!principal.IsInRole("Manager") && !principal.IsInRole("Admin"))
        {
            return Results.Forbid();
        }

        var customerLogin = request.CustomerName.Trim();
        if (string.IsNullOrEmpty(customerLogin))
        {
            return Results.BadRequest("Customer name (account username) is required.");
        }

        var ownerUsername = customerLogin.ToLowerInvariant();
        var correlationId = httpContext.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        var bearer = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        var (sale, error) = await sagaService.StartCreateSaleAsync(
            request,
            ownerUsername,
            correlationId,
            string.IsNullOrWhiteSpace(bearer) ? null : bearer,
            ct);
        if (error is not null)
        {
            return error;
        }

        return Results.Created($"/api/sales/{sale!.Id}", sale);
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
}
