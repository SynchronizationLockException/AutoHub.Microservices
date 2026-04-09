using BuildingBlocks.Hosting.Persistence;
using PaymentService.Api.Data;
using PaymentService.Api.Models;

namespace PaymentService.Api.Messaging;

public sealed class PaymentOutboxPublisher(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<PaymentOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await OutboxPublisherExecutor.RunAsync<PaymentDbContext, OutboxMessage>(
            services,
            configuration,
            logger,
            routingKey: "payment.completed",
            stoppingToken);
    }
}
