namespace BuildingBlocks.Messaging.Inbox;

public interface IProcessedMessage
{
    Guid Id { get; init; }
    string MessageId { get; init; }
    DateTime ProcessedAtUtc { get; init; }
}
