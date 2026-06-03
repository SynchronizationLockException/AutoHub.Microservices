namespace BuildingBlocks.Contracts;

public sealed record RentalCreatedEvent(Guid CarId, Guid RentalId, Guid ReservationId);

public sealed record SaleCreatedEvent(Guid CarId, Guid SaleId, Guid ReservationId);

public sealed record RentalCancelledEvent(Guid CarId, Guid RentalId, Guid ReservationId);

public sealed record SaleCancelledEvent(Guid CarId, Guid SaleId, Guid ReservationId);

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    string ReferenceKind,
    Guid ReferenceId,
    decimal Amount,
    string Currency);
