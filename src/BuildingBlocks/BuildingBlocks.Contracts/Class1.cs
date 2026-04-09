namespace BuildingBlocks.Contracts;

public sealed record RentalCreatedEvent(Guid CarId, Guid RentalId);

public sealed record SaleCreatedEvent(Guid CarId, Guid SaleId);

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    string ReferenceKind,
    Guid ReferenceId,
    decimal Amount,
    string Currency);
