namespace NotificationService.Api.Models;

public sealed class ProcessedMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string MessageId { get; init; }
}
