using RentalService.Api.Services;

namespace RentalService.Api.Messaging;

public sealed class SagaTimeoutWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<SagaTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeoutMinutes = configuration.GetValue("Saga:PublishedTimeoutMinutes", 15);
        var delay = TimeSpan.FromSeconds(configuration.GetValue("Saga:PollIntervalSeconds", 30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var saga = scope.ServiceProvider.GetRequiredService<RentalSagaService>();
                await saga.ProcessTimeoutsAsync(TimeSpan.FromMinutes(timeoutMinutes), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Rental saga timeout worker failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
