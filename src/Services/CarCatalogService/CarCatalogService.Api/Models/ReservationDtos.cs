namespace CarCatalogService.Api.Models;

public sealed record CreateReservationRequest(string Purpose, string HolderReference, int? TtlMinutes);

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid CarId,
    string Purpose,
    string Status,
    string HolderReference,
    DateTime ExpiresAtUtc);
