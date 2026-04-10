using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace BuildingBlocks.Hosting.Persistence;

public interface IOutboxMessageRecord
{
    Guid Id { get; init; }
    string Payload { get; init; }
    DateTime? ProcessedOnUtc { get; set; }
}

public static class OutboxPublisherExecutor
{
    public static async Task RunAsync<TDbContext, TOutboxMessage>(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        string routingKey,
        CancellationToken stoppingToken)
        where TDbContext : DbContext
        where TOutboxMessage : class, IOutboxMessageRecord
    {
        const string selectPendingOutboxSql =
            """
            SELECT * FROM "OutboxMessages"
            WHERE "ProcessedOnUtc" IS NULL
            ORDER BY "OccurredOnUtc"
            LIMIT 20
            FOR UPDATE SKIP LOCKED
            """;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration.GetRequiredValue("RabbitMq:Host"),
                    UserName = configuration.GetRequiredValue("RabbitMq:Username"),
                    Password = configuration.GetRequiredValue("RabbitMq:Password")
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.ExchangeDeclare("autohub.events", ExchangeType.Topic, durable: true);

                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);

                    try
                    {
                        var messages = await db.Set<TOutboxMessage>()
                            .FromSqlRaw(selectPendingOutboxSql)
                            .ToListAsync(stoppingToken);

                        if (messages.Count == 0)
                        {
                            await tx.CommitAsync(stoppingToken);
                            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            var body = Encoding.UTF8.GetBytes(message.Payload);
                            var properties = channel.CreateBasicProperties();
                            properties.Persistent = true;
                            properties.MessageId = message.Id.ToString();
                            channel.BasicPublish("autohub.events", routingKey, properties, body);
                            message.ProcessedOnUtc = DateTime.UtcNow;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        await tx.CommitAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        try
                        {
                            await tx.RollbackAsync(stoppingToken);
                        }
                        catch (Exception rbEx)
                        {
                            logger.LogWarning(rbEx, "Rollback failed after outbox publish error.");
                        }

                        logger.LogError(ex, "Outbox publish batch failed; retrying after delay.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher RabbitMQ connection failed; reconnecting after delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
