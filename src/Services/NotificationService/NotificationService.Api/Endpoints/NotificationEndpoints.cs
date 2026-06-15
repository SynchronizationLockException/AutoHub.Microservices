using BuildingBlocks.Hosting;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using System.Security.Claims;

namespace NotificationService.Api.Endpoints;

public static class NotificationEndpoints
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/deliveries", ListDeliveriesAsync).RequireAuthorization();

        app.MapGet("/api/deliveries", ListDeliveriesAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListDeliveriesAsync(
        ClaimsPrincipal principal,
        int? take,
        NotificationDbContext db,
        CancellationToken ct)
    {
        if (!OwnerScopeExtensions.TryGetOwnerScope(principal, out var deny, out var ownerFilter))
        {
            return deny!;
        }

        var n = Math.Clamp(take.GetValueOrDefault(DefaultTake), 1, MaxTake);
        var query = db.NotificationDeliveries.AsNoTracking();
        if (ownerFilter is not null)
        {
            query = query.Where(x => x.OwnerUsername == ownerFilter);
        }

        var deliveries = await query
            .OrderByDescending(x => x.DeliveredOnUtc)
            .Take(n)
            .ToListAsync(ct);

        return Results.Ok(deliveries);
    }
}
