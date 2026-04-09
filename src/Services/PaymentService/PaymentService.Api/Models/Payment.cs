namespace PaymentService.Api.Models;

public sealed class Payment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OwnerUsername { get; init; } = string.Empty;
    public PaymentReferenceKind ReferenceKind { get; init; }
    public Guid ReferenceId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public PaymentStatus Status { get; init; } = PaymentStatus.Completed;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
