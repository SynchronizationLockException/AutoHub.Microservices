namespace SalesService.Api.Models;

public sealed class SagaInstance
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CorrelationId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string StepDataJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SagaStepData
{
    public Guid? ReservationId { get; set; }
    public Guid? CarId { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? OutboxMessageId { get; set; }
    public string? LastError { get; set; }
}

public static class SaleStatuses
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
}
