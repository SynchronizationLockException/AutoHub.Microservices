using BuildingBlocks.Hosting.Persistence;

namespace RentalService.Api.Models;

public sealed class OutboxMessage : IOutboxTypedMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedOnUtc { get; set; }
}
