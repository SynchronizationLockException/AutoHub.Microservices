namespace CarCatalogService.Api.Models;

public sealed class Car
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Vin { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int Year { get; init; }
    public decimal PricePerDay { get; set; }
    public decimal SalePrice { get; set; }
    public bool IsAvailableForRent { get; set; } = true;
    public bool IsAvailableForSale { get; set; } = true;
}
