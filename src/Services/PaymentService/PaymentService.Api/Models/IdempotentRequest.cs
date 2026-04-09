using BuildingBlocks.Hosting.Persistence;

namespace PaymentService.Api.Models;

public sealed class IdempotentRequest : IIdempotentRequestRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string KeyHash { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
}
