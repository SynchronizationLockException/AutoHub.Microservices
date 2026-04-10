using BuildingBlocks.Contracts;
using BuildingBlocks.Hosting;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace NotificationService.Api.Messaging;

public sealed class NotificationEventsConsumer(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<NotificationEventsConsumer> logger) : BackgroundService
{
    private const string QueueName = "notifications.delivery";
    private const string RetryQueueName = "notifications.delivery.retry";
    private const string DeadQueueName = "notifications.delivery.dead";
    private const string ExchangeName = "autohub.events";
    private const string RetryExchange = "autohub.retry";
    private const string DeadExchange = "autohub.dead";
    private const string RetryRoutingKey = "notifications.delivery";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = configuration.GetRequiredValue("RabbitMq:Host");
        var username = configuration.GetRequiredValue("RabbitMq:Username");
        var password = configuration.GetRequiredValue("RabbitMq:Password");
        var prefetchCount = configuration.GetValue<ushort?>("RabbitMq:NotificationConsumerPrefetchCount") ?? 8;
        var maxRetryCount = configuration.GetValue<int?>("RabbitMq:NotificationConsumerMaxRetryCount") ?? 2;
        var handlerConcurrency = configuration.GetValue<int?>("RabbitMq:NotificationConsumerConcurrency") ?? 4;
        if (handlerConcurrency < 1)
        {
            throw new InvalidOperationException("RabbitMq:NotificationConsumerConcurrency must be >= 1.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = host,
                    UserName = username,
                    Password = password,
                    DispatchConsumersAsync = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.BasicQos(0, prefetchCount, global: false);
                channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
                channel.ExchangeDeclare(RetryExchange, ExchangeType.Direct, durable: true);
                channel.ExchangeDeclare(DeadExchange, ExchangeType.Direct, durable: true);

                channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.QueueDeclare(RetryQueueName, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
                {
                    ["x-message-ttl"] = 10000,
                    ["x-dead-letter-exchange"] = ExchangeName
                });
                channel.QueueDeclare(DeadQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

                channel.QueueBind(QueueName, ExchangeName, "rental.created");
                channel.QueueBind(QueueName, ExchangeName, "sale.created");
                channel.QueueBind(QueueName, ExchangeName, "payment.completed");
                channel.QueueBind(RetryQueueName, RetryExchange, RetryRoutingKey);
                channel.QueueBind(DeadQueueName, DeadExchange, RetryRoutingKey);

                var channelLock = new object();
                var gate = new SemaphoreSlim(handlerConcurrency, handlerConcurrency);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, ea) =>
                {
                    await gate.WaitAsync(stoppingToken);
                    try
                    {
                        await ProcessMessageAsync(channel, channelLock, ea, maxRetryCount, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (channelLock)
                        {
                            channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled error in notification consumer handler.");
                        lock (channelLock)
                        {
                            channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                };

                channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification consumer connection/loop failed; reconnecting after delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        IModel channel,
        object channelLock,
        BasicDeliverEventArgs ea,
        int maxRetryCount,
        CancellationToken stoppingToken)
    {
        var messageId = ea.BasicProperties.MessageId ?? $"{ea.RoutingKey}:{Convert.ToHexString(ea.Body.ToArray())}";
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var alreadyProcessed = await db.ProcessedMessages.AnyAsync(x => x.MessageId == messageId, stoppingToken);
        if (alreadyProcessed)
        {
            lock (channelLock)
            {
                channel.BasicAck(ea.DeliveryTag, false);
            }
            return;
        }

        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
        var routingKey = ea.RoutingKey ?? "";

        try
        {
            LogAndDispatch(routingKey, payload);

            db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId });
            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                RoutingKey = routingKey,
                PayloadJson = payload,
                DeliveredOnUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(stoppingToken);

            lock (channelLock)
            {
                channel.BasicAck(ea.DeliveryTag, false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var retryCount = TryGetRetryCount(ea.BasicProperties.Headers);
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = ea.BasicProperties.MessageId;
            props.Headers = new Dictionary<string, object>
            {
                ["x-retry"] = Encoding.UTF8.GetBytes((retryCount + 1).ToString())
            };
            var targetExchange = retryCount >= maxRetryCount ? DeadExchange : RetryExchange;

            try
            {
                lock (channelLock)
                {
                    channel.BasicPublish(targetExchange, RetryRoutingKey, props, ea.Body);
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception publishEx)
            {
                logger.LogWarning(publishEx, "Failed to republish failed notification message; nacking for requeue.");
                lock (channelLock)
                {
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            }
        }
    }

    private void LogAndDispatch(string routingKey, string payload)
    {
        switch (routingKey)
        {
            case "rental.created":
                if (JsonSerializer.Deserialize<RentalCreatedEvent>(payload) is { } rental)
                {
                    logger.LogInformation(
                        "Notification: rental created. CarId={CarId} RentalId={RentalId}",
                        rental.CarId,
                        rental.RentalId);
                }
                else
                {
                    logger.LogWarning("Notification: could not deserialize rental.created payload.");
                }
                break;
            case "sale.created":
                if (JsonSerializer.Deserialize<SaleCreatedEvent>(payload) is { } sale)
                {
                    logger.LogInformation(
                        "Notification: sale created. CarId={CarId} SaleId={SaleId}",
                        sale.CarId,
                        sale.SaleId);
                }
                else
                {
                    logger.LogWarning("Notification: could not deserialize sale.created payload.");
                }
                break;
            case "payment.completed":
                if (JsonSerializer.Deserialize<PaymentCompletedEvent>(payload) is { } payment)
                {
                    logger.LogInformation(
                        "Notification: payment completed. PaymentId={PaymentId} ReferenceKind={ReferenceKind} ReferenceId={ReferenceId} Amount={Amount} Currency={Currency}",
                        payment.PaymentId,
                        payment.ReferenceKind,
                        payment.ReferenceId,
                        payment.Amount,
                        payment.Currency);
                }
                else
                {
                    logger.LogWarning("Notification: could not deserialize payment.completed payload.");
                }
                break;
            default:
                logger.LogInformation("Notification: unhandled routing key {RoutingKey}", routingKey);
                break;
        }
    }

    private static int TryGetRetryCount(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry", out var raw))
        {
            return 0;
        }

        if (raw is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsedBytes))
        {
            return parsedBytes;
        }

        if (raw is int parsedInt)
        {
            return parsedInt;
        }

        if (raw is string text && int.TryParse(text, out var parsedString))
        {
            return parsedString;
        }

        return 0;
    }
}
