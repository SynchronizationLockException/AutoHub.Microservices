namespace SalesService.Api.Models;

public sealed class SaleOrder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CarId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime SoldAtUtc { get; init; } = DateTime.UtcNow;
    public Guid ReservationId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = SaleStatuses.Active;
}
