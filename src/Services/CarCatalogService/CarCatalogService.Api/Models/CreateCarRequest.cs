namespace CarCatalogService.Api.Models;

public sealed record CreateCarRequest(
    string Vin,
    string Brand,
    string Model,
    int Year,
    decimal PricePerDay,
    decimal SalePrice)
{
    public Car ToCar() =>
        new()
        {
            Vin = Vin,
            Brand = Brand,
            Model = Model,
            Year = Year,
            PricePerDay = PricePerDay,
            SalePrice = SalePrice
        };
}
