namespace RentalService.Api.Models;

public sealed class RentalContract
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CarId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal PricePerDay { get; init; }
    public decimal TotalPrice { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public Guid ReservationId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = RentalStatuses.Active;
}
