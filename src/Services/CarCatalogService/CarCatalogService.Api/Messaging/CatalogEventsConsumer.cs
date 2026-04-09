using BuildingBlocks.Contracts;
using CarCatalogService.Api.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace CarCatalogService.Api.Messaging;

public sealed class CatalogEventsConsumer(IServiceProvider services, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMq:Host"] ?? "rabbitmq",
                    UserName = configuration["RabbitMq:Username"] ?? "guest",
                    Password = configuration["RabbitMq:Password"] ?? "guest"
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.ExchangeDeclare("autohub.events", ExchangeType.Topic, durable: true);
                channel.ExchangeDeclare("autohub.retry", ExchangeType.Direct, durable: true);
                channel.ExchangeDeclare("autohub.dead", ExchangeType.Direct, durable: true);

                channel.QueueDeclare("catalog.availability", durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.QueueDeclare("catalog.availability.retry", durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
                {
                    ["x-message-ttl"] = 10000,
                    ["x-dead-letter-exchange"] = "autohub.events"
                });
                channel.QueueDeclare("catalog.availability.dead", durable: true, exclusive: false, autoDelete: false, arguments: null);

                channel.QueueBind("catalog.availability", "autohub.events", "rental.created");
                channel.QueueBind("catalog.availability", "autohub.events", "sale.created");
                channel.QueueBind("catalog.availability.retry", "autohub.retry", "catalog.availability");
                channel.QueueBind("catalog.availability.dead", "autohub.dead", "catalog.availability");

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (_, ea) =>
                {
                    try
                    {
                        var messageId = ea.BasicProperties.MessageId ?? $"{ea.RoutingKey}:{Convert.ToHexString(ea.Body.ToArray())}";
                        using var scope = services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<CarCatalogDbContext>();

                        var alreadyProcessed = await db.ProcessedMessages.AnyAsync(x => x.MessageId == messageId, stoppingToken);
                        if (alreadyProcessed)
                        {
                            channel.BasicAck(ea.DeliveryTag, false);
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
                            channel.BasicAck(ea.DeliveryTag, false);
                            return;
                        }

                        var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == carId.Value, stoppingToken);
                        if (car is not null)
                        {
                            car.IsAvailableForRent = false;
                            car.IsAvailableForSale = false;
                        }

                        db.ProcessedMessages.Add(new Models.ProcessedMessage { MessageId = messageId });
                        await db.SaveChangesAsync(stoppingToken);
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch
                    {
                        var retryCount = 0;
                        if (ea.BasicProperties.Headers is not null && ea.BasicProperties.Headers.TryGetValue("x-retry", out var retryHeader))
                        {
                            retryCount = Convert.ToInt32(Encoding.UTF8.GetString((byte[])retryHeader));
                        }

                        var props = channel.CreateBasicProperties();
                        props.Persistent = true;
                        props.MessageId = ea.BasicProperties.MessageId;
                        props.Headers = new Dictionary<string, object>
                        {
                            ["x-retry"] = Encoding.UTF8.GetBytes((retryCount + 1).ToString())
                        };

                        var targetExchange = retryCount >= 2 ? "autohub.dead" : "autohub.retry";
                        channel.BasicPublish(targetExchange, "catalog.availability", props, ea.Body);
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                };

                channel.BasicConsume("catalog.availability", autoAck: false, consumer: consumer);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
