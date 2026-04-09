namespace CarCatalogService.Api.Models;

public sealed class ProcessedMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string MessageId { get; init; } = string.Empty;
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}
