using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Models;
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
        var log = scope.ServiceProvider.GetRequiredService<ILogger<NotificationEventsConsumer>>();

        return await InboxProcessor.TryProcessAsync<NotificationDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                var text = routingKey switch
                {
                    "rental.created" => $"Rental created: {payload}",
                    "sale.created" => $"Sale created: {payload}",
                    "payment.completed" => FormatPayment(payload),
                    "rental.cancelled" => $"Rental cancelled: {payload}",
                    "sale.cancelled" => $"Sale cancelled: {payload}",
                    _ => $"Event {routingKey}: {payload}"
                };

                log.LogInformation("Notification: {Text}", text);
                await Task.CompletedTask;
            },
            () => new ProcessedMessage { MessageId = messageId },
            cancellationToken);
    }

    private static string FormatPayment(string payload)
    {
        var evt = JsonSerializer.Deserialize<PaymentCompletedEvent>(payload);
        return evt is null
            ? $"Payment event: {payload}"
            : $"Payment completed {evt.PaymentId} amount {evt.Amount} {evt.Currency}";
    }
}
