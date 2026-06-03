using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

namespace BuildingBlocks.Messaging.Consumers;

public abstract class RabbitMqConsumerHost(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger,
    RabbitMqConsumerOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.HandlerConcurrency < 1)
        {
            throw new InvalidOperationException("HandlerConcurrency must be >= 1.");
        }

        var host = configuration.GetRequiredValue("RabbitMq:Host");
        var username = configuration.GetRequiredValue("RabbitMq:Username");
        var password = configuration.GetRequiredValue("RabbitMq:Password");

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
                channel.BasicQos(0, options.PrefetchCount, global: false);
                DeclareTopology(channel, options);

                var channelLock = new object();
                var gate = new SemaphoreSlim(options.HandlerConcurrency, options.HandlerConcurrency);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, ea) =>
                {
                    await gate.WaitAsync(stoppingToken);
                    try
                    {
                        await HandleDeliveryAsync(channel, channelLock, ea, stoppingToken);
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
                        logger.LogError(ex, "Unhandled error in RabbitMQ consumer {Queue}.", options.QueueName);
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

                channel.BasicConsume(options.QueueName, autoAck: false, consumer: consumer);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ consumer {Queue} connection failed; reconnecting.", options.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    protected virtual void DeclareTopology(IModel channel, RabbitMqConsumerOptions consumerOptions)
    {
        channel.ExchangeDeclare(consumerOptions.ExchangeName, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(consumerOptions.RetryExchange, ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare(consumerOptions.DeadExchange, ExchangeType.Direct, durable: true);

        channel.QueueDeclare(consumerOptions.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueDeclare(consumerOptions.RetryQueueName, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
        {
            ["x-message-ttl"] = consumerOptions.RetryQueueTtlMs,
            ["x-dead-letter-exchange"] = consumerOptions.ExchangeName
        });
        channel.QueueDeclare(consumerOptions.DeadQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        foreach (var routingKey in consumerOptions.RoutingKeys)
        {
            channel.QueueBind(consumerOptions.QueueName, consumerOptions.ExchangeName, routingKey);
        }

        channel.QueueBind(consumerOptions.RetryQueueName, consumerOptions.RetryExchange, consumerOptions.RetryRoutingKey);
        channel.QueueBind(consumerOptions.DeadQueueName, consumerOptions.DeadExchange, consumerOptions.RetryRoutingKey);
    }

    private async Task HandleDeliveryAsync(
        IModel channel,
        object channelLock,
        BasicDeliverEventArgs ea,
        CancellationToken stoppingToken)
    {
        using var activity = MessagingActivitySource.Instance.StartActivity("rabbitmq.consume");
        activity?.SetTag("messaging.routing_key", ea.RoutingKey);
        activity?.SetTag("messaging.queue", options.QueueName);

        var messageId = ea.BasicProperties.MessageId ?? $"{ea.RoutingKey}:{Convert.ToHexString(ea.Body.ToArray())}";
        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            var handled = await ProcessMessageAsync(ea.RoutingKey, messageId, payload, stoppingToken);
            if (!handled)
            {
                lock (channelLock)
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                return;
            }

            lock (channelLock)
            {
                channel.BasicAck(ea.DeliveryTag, false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Message processing failed for {MessageId}.", messageId);
            var retryCount = RabbitMqRetryHelper.TryGetRetryCount(ea.BasicProperties.Headers);
            try
            {
                lock (channelLock)
                {
                    RabbitMqRetryHelper.PublishToRetryOrDead(channel, options, ea, retryCount, options.MaxRetryCount);
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception publishEx)
            {
                logger.LogWarning(publishEx, "Failed to republish message {MessageId}.", messageId);
                lock (channelLock)
                {
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            }
        }
    }

    protected IServiceProvider Services => services;

    protected abstract Task<bool> ProcessMessageAsync(
        string routingKey,
        string messageId,
        string payload,
        CancellationToken cancellationToken);
}
