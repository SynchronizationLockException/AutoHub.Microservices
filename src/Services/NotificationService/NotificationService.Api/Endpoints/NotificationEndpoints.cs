using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;

namespace NotificationService.Api.Endpoints;

public static class NotificationEndpoints
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/deliveries", async (int? take, NotificationDbContext db, CancellationToken ct) =>
        {
            var n = Math.Clamp(take.GetValueOrDefault(DefaultTake), 1, MaxTake);
            var deliveries = await db.NotificationDeliveries
                .AsNoTracking()
                .OrderByDescending(x => x.DeliveredOnUtc)
                .Take(n)
                .ToListAsync(ct);

            return Results.Ok(deliveries);
        }).RequireAuthorization();
    }
}
