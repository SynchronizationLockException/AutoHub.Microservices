using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Models;
using NotificationService.Api.Services;
using System.Text.Json;

namespace NotificationService.Api.Messaging;

public sealed class NotificationEventsConsumer(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<NotificationEventsConsumer> logger)
    : RabbitMqConsumerHost(
        services,
        configuration,
        logger,
        new RabbitMqConsumerOptions
        {
            QueueName = "notifications.delivery",
            RetryQueueName = "notifications.delivery.retry",
            DeadQueueName = "notifications.delivery.dead",
            RetryRoutingKey = "notifications.delivery",
            RoutingKeys =
            [
                "rental.created",
                "sale.created",
                "payment.completed",
                "rental.cancelled",
                "sale.cancelled"
            ]
        })
{
    protected override async Task<bool> ProcessMessageAsync(
        string routingKey,
        string messageId,
        string payload,
        CancellationToken cancellationToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        return await InboxProcessor.TryProcessAsync<NotificationDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                var (text, ownerUsername) = FormatMessage(routingKey, payload);
                var result = await sender.SendAsync(routingKey, text, ownerUsername, token);

                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    RoutingKey = routingKey,
                    PayloadJson = payload,
                    OwnerUsername = ownerUsername,
                    Channel = result.Channel,
                    Status = result.Success ? NotificationStatuses.Delivered : NotificationStatuses.Failed,
                    Detail = result.Detail,
                    DeliveredOnUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(token);
            },
            () => new ProcessedMessage { MessageId = messageId },
            cancellationToken);
    }

    private static (string Text, string OwnerUsername) FormatMessage(string routingKey, string payload)
    {
        switch (routingKey)
        {
            case "rental.created":
            {
                var evt = JsonSerializer.Deserialize<RentalCreatedEvent>(payload);
                return evt is null
                    ? ($"Rental created: {payload}", "unknown")
                    : ($"Rental {evt.RentalId} created for car {evt.CarId}", evt.OwnerUsername);
            }
            case "sale.created":
            {
                var evt = JsonSerializer.Deserialize<SaleCreatedEvent>(payload);
                return evt is null
                    ? ($"Sale created: {payload}", "unknown")
                    : ($"Sale {evt.SaleId} created for car {evt.CarId}", evt.OwnerUsername);
            }
            case "payment.completed":
            {
                var evt = JsonSerializer.Deserialize<PaymentCompletedEvent>(payload);
                return evt is null
                    ? ($"Payment event: {payload}", "unknown")
                    : ($"Payment {evt.PaymentId} completed amount {evt.Amount} {evt.Currency}", evt.OwnerUsername);
            }
            case "rental.cancelled":
            {
                var evt = JsonSerializer.Deserialize<RentalCancelledEvent>(payload);
                return evt is null
                    ? ($"Rental cancelled: {payload}", "unknown")
                    : ($"Rental {evt.RentalId} cancelled", evt.OwnerUsername);
            }
            case "sale.cancelled":
            {
                var evt = JsonSerializer.Deserialize<SaleCancelledEvent>(payload);
                return evt is null
                    ? ($"Sale cancelled: {payload}", "unknown")
                    : ($"Sale {evt.SaleId} cancelled", evt.OwnerUsername);
            }
            default:
                return ($"Event {routingKey}: {payload}", "unknown");
        }
    }
}
