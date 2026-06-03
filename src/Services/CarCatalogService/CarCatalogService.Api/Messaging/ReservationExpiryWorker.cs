using CarCatalogService.Api.Services;

namespace CarCatalogService.Api.Messaging;

public sealed class ReservationExpiryWorker(
    IServiceProvider services,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var reservations = scope.ServiceProvider.GetRequiredService<ReservationService>();
                await reservations.ExpireStaleReservationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reservation expiry worker failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
