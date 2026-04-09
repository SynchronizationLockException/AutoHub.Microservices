namespace SalesService.Api.Models;

public sealed record CreateSaleRequest(Guid CarId, string CustomerName)
{
    public SaleOrder ToSale(decimal price, string ownerUsername) =>
        new()
        {
            CarId = CarId,
            OwnerUsername = ownerUsername,
            CustomerName = CustomerName,
            Price = price
        };
}

public sealed record CatalogCar(Guid Id, decimal SalePrice, bool IsAvailableForSale);
