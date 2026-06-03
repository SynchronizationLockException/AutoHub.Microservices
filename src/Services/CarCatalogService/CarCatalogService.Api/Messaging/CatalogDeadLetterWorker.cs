using BuildingBlocks.Contracts;
using BuildingBlocks.Hosting;
using BuildingBlocks.Messaging.Consumers;
using RabbitMQ.Client;
using System.Net.Http.Json;
using System.Text.Json;

namespace CarCatalogService.Api.Messaging;

public sealed class CatalogDeadLetterWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<CatalogDeadLetterWorker> logger)
    : RabbitMqConsumerHost(
        services,
        configuration,
        logger,
        new RabbitMqConsumerOptions
        {
            QueueName = "catalog.availability.dead",
            RetryQueueName = "catalog.availability.dead.retry",
            DeadQueueName = "catalog.availability.dead.final",
            RetryRoutingKey = "catalog.availability",
            RoutingKeys = [],
            MaxRetryCount = 0,
            HandlerConcurrency = 1
        })
{
    protected override void DeclareTopology(IModel channel, RabbitMqConsumerOptions consumerOptions)
    {
        channel.QueueDeclare(consumerOptions.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override async Task<bool> ProcessMessageAsync(
        string routingKey,
        string messageId,
        string payload,
        CancellationToken cancellationToken)
    {
        var secret = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var rental = JsonSerializer.Deserialize<RentalCreatedEvent>(payload);
        if (rental is not null && rental.RentalId != Guid.Empty)
        {
            await RequestCompensationAsync(
                configuration["ExternalServices:RentalApiBaseUrl"],
                "rental",
                rental.RentalId,
                rental.ReservationId,
                rental.CarId,
                secret,
                cancellationToken);
            return true;
        }

        var sale = JsonSerializer.Deserialize<SaleCreatedEvent>(payload);
        if (sale is not null && sale.SaleId != Guid.Empty)
        {
            await RequestCompensationAsync(
                configuration["ExternalServices:SalesApiBaseUrl"],
                "sale",
                sale.SaleId,
                sale.ReservationId,
                sale.CarId,
                secret,
                cancellationToken);
            return true;
        }

        return false;
    }

    private async Task RequestCompensationAsync(
        string? baseUrl,
        string kind,
        Guid entityId,
        Guid reservationId,
        Guid carId,
        string secret,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient($"{kind}-internal");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/internal/sagas/compensate");
        request.Headers.Add(InternalApiExtensions.SecretHeaderName, secret);
        request.Content = JsonContent.Create(new
        {
            kind,
            entityId,
            reservationId,
            carId
        });

        try
        {
            await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DLQ compensation request failed for {Kind} {EntityId}.", kind, entityId);
            throw;
        }
    }
}
