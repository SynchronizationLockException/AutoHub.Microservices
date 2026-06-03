namespace CarCatalogService.Api.Models;

public static class ReservationPurposes
{
    public const string Rent = "Rent";
    public const string Sale = "Sale";
}

public static class ReservationStatuses
{
    public const string Active = "Active";
    public const string Confirmed = "Confirmed";
    public const string Released = "Released";
    public const string Expired = "Expired";
}

public sealed class CarReservation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CarId { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string HolderReference { get; init; } = string.Empty;
    public string Status { get; set; } = ReservationStatuses.Active;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; init; }
}
