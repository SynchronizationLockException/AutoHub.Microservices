namespace BuildingBlocks.Hosting.Persistence;

public static class OutboxRouting
{
    public static string Map(string messageType) =>
        messageType switch
        {
            "RentalCreated" => "rental.created",
            "RentalCancelled" => "rental.cancelled",
            "SaleCreated" => "sale.created",
            "SaleCancelled" => "sale.cancelled",
            "PaymentCompleted" => "payment.completed",
            _ => messageType.Replace('.', '_').ToLowerInvariant()
        };
}
