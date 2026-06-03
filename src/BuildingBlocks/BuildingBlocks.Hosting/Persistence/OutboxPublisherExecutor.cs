using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace BuildingBlocks.Hosting.Persistence;

public interface IOutboxMessageRecord
{
    Guid Id { get; init; }
    string Payload { get; init; }
    DateTime? ProcessedOnUtc { get; set; }
}

public interface IOutboxTypedMessage : IOutboxMessageRecord
{
    string Type { get; init; }
}

public static class OutboxPublisherExecutor
{
    private static readonly Meter Meter = new("BuildingBlocks.Hosting");
    private static readonly Histogram<double> BatchDurationMs =
        Meter.CreateHistogram<double>("outbox.batch.duration.ms", unit: "ms");
    private static readonly Histogram<int> BatchSize =
        Meter.CreateHistogram<int>("outbox.batch.size");
    private static readonly Histogram<double> PollDelayMs =
        Meter.CreateHistogram<double>("outbox.poll.delay.ms", unit: "ms");
    private static readonly Counter<long> PublishedMessages =
        Meter.CreateCounter<long>("outbox.messages.published");
    private static readonly Counter<long> PublishErrors =
        Meter.CreateCounter<long>("outbox.publish.errors");

    public static async Task RunAsync<TDbContext, TOutboxMessage>(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken stoppingToken)
        where TDbContext : DbContext
        where TOutboxMessage : class, IOutboxTypedMessage
    {
        var maxBatchSize = Math.Clamp(configuration.GetValue("Outbox:MaxBatchSize", 100), 20, 500);
        var idleDelay = TimeSpan.FromMilliseconds(Math.Clamp(configuration.GetValue("Outbox:IdleDelayMs", 2000), 200, 5000));
        var busyDelay = TimeSpan.FromMilliseconds(Math.Clamp(configuration.GetValue("Outbox:BusyDelayMs", 100), 25, 1000));
        var failureDelay = TimeSpan.FromMilliseconds(Math.Clamp(configuration.GetValue("Outbox:FailureDelayMs", 3000), 500, 10000));
        const string selectPendingOutboxSql =
            """
            SELECT * FROM "OutboxMessages"
            WHERE "ProcessedOnUtc" IS NULL
            ORDER BY "OccurredOnUtc"
            LIMIT {0}
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
                        var batchStopwatch = Stopwatch.StartNew();
                        var messages = await db.Set<TOutboxMessage>()
                            .FromSqlRaw(selectPendingOutboxSql, maxBatchSize)
                            .ToListAsync(stoppingToken);

                        BatchSize.Record(messages.Count);
                        if (messages.Count == 0)
                        {
                            await tx.CommitAsync(stoppingToken);
                            PollDelayMs.Record(idleDelay.TotalMilliseconds);
                            await Task.Delay(idleDelay, stoppingToken);
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            var body = Encoding.UTF8.GetBytes(message.Payload);
                            var properties = channel.CreateBasicProperties();
                            properties.Persistent = true;
                            properties.MessageId = message.Id.ToString();
                            var routingKey = OutboxRouting.Map(message.Type);
                            channel.BasicPublish("autohub.events", routingKey, properties, body);
                            message.ProcessedOnUtc = DateTime.UtcNow;
                            PublishedMessages.Add(1);
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        await tx.CommitAsync(stoppingToken);
                        batchStopwatch.Stop();
                        BatchDurationMs.Record(batchStopwatch.Elapsed.TotalMilliseconds);
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
                        PublishErrors.Add(1);
                        PollDelayMs.Record(failureDelay.TotalMilliseconds);
                        await Task.Delay(failureDelay, stoppingToken);
                        continue;
                    }

                    PollDelayMs.Record(busyDelay.TotalMilliseconds);
                    await Task.Delay(busyDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher RabbitMQ connection failed; reconnecting after delay.");
                PublishErrors.Add(1);
                PollDelayMs.Record(failureDelay.TotalMilliseconds);
                await Task.Delay(failureDelay, stoppingToken);
            }
        }
    }
}
