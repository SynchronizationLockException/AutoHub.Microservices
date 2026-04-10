namespace NotificationService.Api.Models;

public sealed class NotificationDelivery
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RoutingKey { get; init; }
    public required string PayloadJson { get; init; }
    public DateTime DeliveredOnUtc { get; init; }
}
