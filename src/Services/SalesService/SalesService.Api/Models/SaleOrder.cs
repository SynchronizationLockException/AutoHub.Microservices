namespace SalesService.Api.Models;

public sealed class SaleOrder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CarId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime SoldAtUtc { get; init; } = DateTime.UtcNow;
}
