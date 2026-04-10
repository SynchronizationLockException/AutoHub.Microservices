using BuildingBlocks.Contracts;
using BuildingBlocks.Hosting.Persistence;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
using RentalService.Api.Models;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RentalService.Api.Endpoints;

public static class RentalEndpoints
{
    public static void MapRentalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rentals", async (ClaimsPrincipal principal, RentalDbContext db, CancellationToken ct) =>
        {
            if (!TryGetOwnerScope(principal, out var deny, out var ownerFilter))
            {
                return deny;
            }

            var query = db.Rentals.AsNoTracking();
            if (ownerFilter is not null)
            {
                query = query.Where(r => r.OwnerUsername == ownerFilter);
            }

            return Results.Ok(await query.ToListAsync(ct));
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
        IHttpClientFactory factory,
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
            action: () => CreateRentalInternalAsync(request, factory, db, principal, ct),
            ct);
    }

    private static async Task<IResult> CreateRentalInternalAsync(
        CreateRentalRequest request,
        IHttpClientFactory factory,
        RentalDbContext db,
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

        var catalogClient = factory.CreateClient("catalog");
        var car = await catalogClient.GetFromJsonAsync<CatalogCar>($"/api/cars/{request.CarId}", ct);
        if (car is null || !car.IsAvailableForRent)
        {
            return Results.BadRequest("Car is not available for rent.");
        }

        var rental = request.ToRental(car.PricePerDay, ownerUsername);
        db.Rentals.Add(rental);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "RentalCreated",
            Payload = JsonSerializer.Serialize(new RentalCreatedEvent(rental.CarId, rental.Id))
        });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/rentals/{rental.Id}", rental);
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
