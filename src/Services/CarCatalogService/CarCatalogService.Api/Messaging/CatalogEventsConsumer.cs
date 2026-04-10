using BuildingBlocks.Contracts;
using BuildingBlocks.Hosting;
using CarCatalogService.Api.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace CarCatalogService.Api.Messaging;

public sealed class CatalogEventsConsumer(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<CatalogEventsConsumer> logger) : BackgroundService
{
    private const string QueueName = "catalog.availability";
    private const string RetryQueueName = "catalog.availability.retry";
    private const string DeadQueueName = "catalog.availability.dead";
    private const string ExchangeName = "autohub.events";
    private const string RetryExchange = "autohub.retry";
    private const string DeadExchange = "autohub.dead";
    private const string RetryRoutingKey = "catalog.availability";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = configuration.GetRequiredValue("RabbitMq:Host");
        var username = configuration.GetRequiredValue("RabbitMq:Username");
        var password = configuration.GetRequiredValue("RabbitMq:Password");
        var prefetchCount = configuration.GetValue<ushort?>("RabbitMq:CatalogConsumerPrefetchCount") ?? 8;
        var maxRetryCount = configuration.GetValue<int?>("RabbitMq:CatalogConsumerMaxRetryCount") ?? 2;
        var handlerConcurrency = configuration.GetValue<int?>("RabbitMq:CatalogConsumerConcurrency") ?? 4;
        if (handlerConcurrency < 1)
        {
            throw new InvalidOperationException("RabbitMq:CatalogConsumerConcurrency must be >= 1.");
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
                        logger.LogError(ex, "Unhandled error in catalog consumer handler.");
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
                logger.LogError(ex, "Catalog consumer connection/loop failed; reconnecting after delay.");
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
        var db = scope.ServiceProvider.GetRequiredService<CarCatalogDbContext>();

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
        var carId = ea.RoutingKey switch
        {
            "rental.created" => JsonSerializer.Deserialize<RentalCreatedEvent>(payload)?.CarId,
            "sale.created" => JsonSerializer.Deserialize<SaleCreatedEvent>(payload)?.CarId,
            _ => null
        };

        if (carId is null)
        {
            lock (channelLock)
            {
                channel.BasicAck(ea.DeliveryTag, false);
            }
            return;
        }

        try
        {
            var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == carId.Value, stoppingToken);
            if (car is not null)
            {
                car.IsAvailableForRent = false;
                car.IsAvailableForSale = false;
            }

            db.ProcessedMessages.Add(new Models.ProcessedMessage { MessageId = messageId });
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
                logger.LogWarning(publishEx, "Failed to republish failed catalog message; nacking for requeue.");
                lock (channelLock)
                {
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            }
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
