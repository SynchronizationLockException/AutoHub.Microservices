using BuildingBlocks.Messaging.Inbox;

namespace NotificationService.Api.Models;

public sealed class ProcessedMessage : IProcessedMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string MessageId { get; init; } = string.Empty;
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}
