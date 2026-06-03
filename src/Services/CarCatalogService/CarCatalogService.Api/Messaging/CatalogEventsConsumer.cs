using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Consumers;
using BuildingBlocks.Messaging.Inbox;
using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using BuildingBlocks.Hosting;
using CarCatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace CarCatalogService.Api.Messaging;

public sealed class CatalogEventsConsumer(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumerHost(
        services,
        configuration,
        logger,
        new RabbitMqConsumerOptions
        {
            QueueName = "catalog.availability",
            RetryQueueName = "catalog.availability.retry",
            DeadQueueName = "catalog.availability.dead",
            RetryRoutingKey = "catalog.availability",
            RoutingKeys =
            [
                "rental.created",
                "sale.created",
                "rental.cancelled",
                "sale.cancelled"
            ]
        })
{
    protected override async Task<bool> ProcessMessageAsync(
        string routingKey,
        string messageId,
        string payload,
        CancellationToken cancellationToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarCatalogDbContext>();
        var reservations = scope.ServiceProvider.GetRequiredService<ReservationService>();
        var completionNotifier = scope.ServiceProvider.GetRequiredService<SagaCompletionNotifier>();

        var alreadyProcessed = await db.ProcessedMessages.AnyAsync(
            x => x.MessageId == messageId,
            cancellationToken);
        if (alreadyProcessed)
        {
            return false;
        }

        return routingKey switch
        {
            "rental.created" => await HandleRentalCreatedAsync(
                db, reservations, completionNotifier, messageId, payload, cancellationToken),
            "sale.created" => await HandleSaleCreatedAsync(
                db, reservations, completionNotifier, messageId, payload, cancellationToken),
            "rental.cancelled" => await HandleRentalCancelledAsync(
                db, reservations, messageId, payload, cancellationToken),
            "sale.cancelled" => await HandleSaleCancelledAsync(
                db, reservations, messageId, payload, cancellationToken),
            _ => false
        };
    }

    private static async Task<bool> HandleRentalCreatedAsync(
        CarCatalogDbContext db,
        ReservationService reservations,
        SagaCompletionNotifier completionNotifier,
        string messageId,
        string payload,
        CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<RentalCreatedEvent>(payload);
        if (evt is null)
        {
            return false;
        }

        return await InboxProcessor.TryProcessAsync<CarCatalogDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                if (evt.ReservationId != Guid.Empty)
                {
                    var (confirmed, error) = await reservations.TryConfirmAsync(evt.CarId, evt.ReservationId, token);
                    if (confirmed is null)
                    {
                        throw new InvalidOperationException(error ?? "Reservation confirm failed.");
                    }

                    var holder = await db.Reservations.AsNoTracking()
                        .Where(x => x.Id == evt.ReservationId)
                        .Select(x => x.HolderReference)
                        .FirstAsync(token);
                    await completionNotifier.NotifyRentalCompletedAsync(evt.RentalId, holder, token);
                    return;
                }

                var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == evt.CarId, token);
                if (car is not null)
                {
                    car.IsAvailableForRent = false;
                    car.IsAvailableForSale = false;
                }
            },
            () => new ProcessedMessage { MessageId = messageId },
            ct);
    }

    private static async Task<bool> HandleSaleCreatedAsync(
        CarCatalogDbContext db,
        ReservationService reservations,
        SagaCompletionNotifier completionNotifier,
        string messageId,
        string payload,
        CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<SaleCreatedEvent>(payload);
        if (evt is null)
        {
            return false;
        }

        return await InboxProcessor.TryProcessAsync<CarCatalogDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                if (evt.ReservationId != Guid.Empty)
                {
                    var (confirmed, error) = await reservations.TryConfirmAsync(evt.CarId, evt.ReservationId, token);
                    if (confirmed is null)
                    {
                        throw new InvalidOperationException(error ?? "Reservation confirm failed.");
                    }

                    var holder = await db.Reservations.AsNoTracking()
                        .Where(x => x.Id == evt.ReservationId)
                        .Select(x => x.HolderReference)
                        .FirstAsync(token);
                    await completionNotifier.NotifySaleCompletedAsync(evt.SaleId, holder, token);
                    return;
                }

                var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == evt.CarId, token);
                if (car is not null)
                {
                    car.IsAvailableForRent = false;
                    car.IsAvailableForSale = false;
                }
            },
            () => new ProcessedMessage { MessageId = messageId },
            ct);
    }

    private static async Task<bool> HandleRentalCancelledAsync(
        CarCatalogDbContext db,
        ReservationService reservations,
        string messageId,
        string payload,
        CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<RentalCancelledEvent>(payload);
        if (evt is null)
        {
            return false;
        }

        return await InboxProcessor.TryProcessAsync<CarCatalogDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                if (evt.ReservationId != Guid.Empty)
                {
                    await reservations.TryReleaseAsync(evt.CarId, evt.ReservationId, token);
                }

                await reservations.ReleaseAvailabilityForCarAsync(evt.CarId, token);
            },
            () => new ProcessedMessage { MessageId = messageId },
            ct);
    }

    private static async Task<bool> HandleSaleCancelledAsync(
        CarCatalogDbContext db,
        ReservationService reservations,
        string messageId,
        string payload,
        CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<SaleCancelledEvent>(payload);
        if (evt is null)
        {
            return false;
        }

        return await InboxProcessor.TryProcessAsync<CarCatalogDbContext, ProcessedMessage>(
            db,
            messageId,
            async token =>
            {
                if (evt.ReservationId != Guid.Empty)
                {
                    await reservations.TryReleaseAsync(evt.CarId, evt.ReservationId, token);
                }

                await reservations.ReleaseAvailabilityForCarAsync(evt.CarId, token);
            },
            () => new ProcessedMessage { MessageId = messageId },
            ct);
    }
}

public sealed class SagaCompletionNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SagaCompletionNotifier> logger)
{
    public async Task NotifyRentalCompletedAsync(Guid rentalId, string holderReference, CancellationToken ct)
    {
        var baseUrl = configuration["ExternalServices:RentalApiBaseUrl"];
        var secret = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        var client = httpClientFactory.CreateClient("rental-internal");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/sagas/complete");
        request.Headers.Add(InternalApiExtensions.SecretHeaderName, secret);
        request.Content = JsonContent.Create(new { holderReference, rentalId, kind = "rental" });
        try
        {
            await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify rental saga completion for {RentalId}.", rentalId);
        }
    }

    public async Task NotifySaleCompletedAsync(Guid saleId, string holderReference, CancellationToken ct)
    {
        var baseUrl = configuration["ExternalServices:SalesApiBaseUrl"];
        var secret = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        var client = httpClientFactory.CreateClient("sales-internal");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/sagas/complete");
        request.Headers.Add(InternalApiExtensions.SecretHeaderName, secret);
        request.Content = JsonContent.Create(new { holderReference, saleId, kind = "sale" });
        try
        {
            await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify sales saga completion for {SaleId}.", saleId);
        }
    }
}
