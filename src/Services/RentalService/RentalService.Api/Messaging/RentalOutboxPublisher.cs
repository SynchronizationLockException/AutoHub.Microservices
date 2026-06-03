using BuildingBlocks.Hosting.Persistence;
using RentalService.Api.Data;
using RentalService.Api.Models;

namespace RentalService.Api.Messaging;

public sealed class RentalOutboxPublisher(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<RentalOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await OutboxPublisherExecutor.RunAsync<RentalDbContext, OutboxMessage>(
            services,
            configuration,
            logger,
            stoppingToken);
    }
}
