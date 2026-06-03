namespace RentalService.Api.Models;

public sealed record CreateRentalRequest(
    Guid CarId,
    string CustomerName,
    DateOnly StartDate,
    DateOnly EndDate)
{
    public RentalContract ToRental(decimal pricePerDay, string ownerUsername)
    {
        var days = Math.Max(1, EndDate.DayNumber - StartDate.DayNumber + 1);
        return new RentalContract
        {
            CarId = CarId,
            OwnerUsername = ownerUsername,
            CustomerName = CustomerName,
            StartDate = StartDate,
            EndDate = EndDate,
            PricePerDay = pricePerDay,
            TotalPrice = pricePerDay * days
        };
    }
}

