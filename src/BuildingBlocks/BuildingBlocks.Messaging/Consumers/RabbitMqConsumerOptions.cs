namespace BuildingBlocks.Messaging.Consumers;

public sealed class RabbitMqConsumerOptions
{
    public required string QueueName { get; init; }
    public required string RetryQueueName { get; init; }
    public required string DeadQueueName { get; init; }
    public required string RetryRoutingKey { get; init; }
    public string ExchangeName { get; init; } = "autohub.events";
    public string RetryExchange { get; init; } = "autohub.retry";
    public string DeadExchange { get; init; } = "autohub.dead";
    public required IReadOnlyList<string> RoutingKeys { get; init; }
    public ushort PrefetchCount { get; init; } = 8;
    public int MaxRetryCount { get; init; } = 2;
    public int HandlerConcurrency { get; init; } = 4;
    public int RetryQueueTtlMs { get; init; } = 10000;
}
