using BuildingBlocks.Hosting.Persistence;
using SalesService.Api.Data;
using SalesService.Api.Models;

namespace SalesService.Api.Messaging;

public sealed class SalesOutboxPublisher(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<SalesOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await OutboxPublisherExecutor.RunAsync<SalesDbContext, OutboxMessage>(
            services,
            configuration,
            logger,
            routingKey: "sale.created",
            stoppingToken);
    }
}
