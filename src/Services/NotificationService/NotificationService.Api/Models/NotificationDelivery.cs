namespace NotificationService.Api.Models;

public sealed class NotificationDelivery
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RoutingKey { get; init; }
    public required string PayloadJson { get; init; }
    public required string OwnerUsername { get; init; }
    public required string Channel { get; init; }
    public required string Status { get; init; }
    public string? Detail { get; init; }
    public DateTime DeliveredOnUtc { get; init; }
}

public static class NotificationChannels
{
    public const string Log = "log";
    public const string Email = "email";
}

public static class NotificationStatuses
{
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
