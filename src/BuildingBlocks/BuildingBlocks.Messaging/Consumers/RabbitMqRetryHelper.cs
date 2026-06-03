using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace BuildingBlocks.Messaging.Consumers;

public static class RabbitMqRetryHelper
{
    public static int TryGetRetryCount(IDictionary<string, object>? headers)
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

    public static void PublishToRetryOrDead(
        IModel channel,
        RabbitMqConsumerOptions options,
        BasicDeliverEventArgs ea,
        int retryCount,
        int maxRetryCount)
    {
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = ea.BasicProperties.MessageId;
        props.Headers = new Dictionary<string, object>
        {
            ["x-retry"] = Encoding.UTF8.GetBytes((retryCount + 1).ToString())
        };
        var targetExchange = retryCount >= maxRetryCount ? options.DeadExchange : options.RetryExchange;
        channel.BasicPublish(targetExchange, options.RetryRoutingKey, props, ea.Body);
    }
}
