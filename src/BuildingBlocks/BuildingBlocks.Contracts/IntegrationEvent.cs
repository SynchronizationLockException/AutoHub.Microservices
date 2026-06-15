namespace BuildingBlocks.Contracts;

public sealed record RentalCreatedEvent(
    Guid CarId,
    Guid RentalId,
    Guid ReservationId,
    string OwnerUsername);

public sealed record SaleCreatedEvent(
    Guid CarId,
    Guid SaleId,
    Guid ReservationId,
    string OwnerUsername);

public sealed record RentalCancelledEvent(
    Guid CarId,
    Guid RentalId,
    Guid ReservationId,
    string OwnerUsername);

public sealed record SaleCancelledEvent(
    Guid CarId,
    Guid SaleId,
    Guid ReservationId,
    string OwnerUsername);

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    string ReferenceKind,
    Guid ReferenceId,
    decimal Amount,
    string Currency,
    string OwnerUsername);
