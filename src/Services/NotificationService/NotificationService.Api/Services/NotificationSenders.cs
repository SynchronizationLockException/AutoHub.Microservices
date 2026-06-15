using NotificationService.Api.Models;

namespace NotificationService.Api.Services;

public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(string routingKey, string message, string ownerUsername, CancellationToken ct);
}

public sealed record NotificationSendResult(bool Success, string Channel, string? Detail);

public sealed class CompositeNotificationSender(IEnumerable<INotificationSender> senders) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(
        string routingKey,
        string message,
        string ownerUsername,
        CancellationToken ct)
    {
        foreach (var sender in senders)
        {
            var result = await sender.SendAsync(routingKey, message, ownerUsername, ct);
            if (result.Success)
            {
                return result;
            }
        }

        return new NotificationSendResult(false, NotificationChannels.Log, "All notification channels failed.");
    }
}

public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task<NotificationSendResult> SendAsync(
        string routingKey,
        string message,
        string ownerUsername,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Notification to {OwnerUsername} [{RoutingKey}]: {Message}",
            ownerUsername,
            routingKey,
            message);
        return Task.FromResult(new NotificationSendResult(true, NotificationChannels.Log, "Logged to application log."));
    }
}

public sealed class EmailNotificationSender(ILogger<EmailNotificationSender> logger) : INotificationSender
{
    public Task<NotificationSendResult> SendAsync(
        string routingKey,
        string message,
        string ownerUsername,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Email notification stub for {OwnerUsername} [{RoutingKey}]: {Message}",
            ownerUsername,
            routingKey,
            message);
        return Task.FromResult(new NotificationSendResult(
            true,
            NotificationChannels.Email,
            $"email-stub:{ownerUsername}@autohub.local"));
    }
}
