using BuildingBlocks.Contracts;
using BuildingBlocks.Saga;
using Microsoft.EntityFrameworkCore;
using SalesService.Api.Data;
using SalesService.Api.Models;
using System.Text.Json;

namespace SalesService.Api.Services;

public sealed class SalesSagaService(SalesDbContext db, CatalogReservationClient catalog)
{
    public async Task<(SaleOrder? Sale, IResult? Error)> StartCreateSaleAsync(
        CreateSaleRequest request,
        string ownerUsername,
        string correlationId,
        string? bearerToken,
        CancellationToken ct)
    {
        var saga = new SagaInstance
        {
            CorrelationId = correlationId,
            Type = SagaTypes.CreateSale,
            State = SagaStates.Reserved,
            StepDataJson = JsonSerializer.Serialize(new SagaStepData { CarId = request.CarId })
        };
        db.SagaInstances.Add(saga);

        var (reservation, reserveError) = await catalog.ReserveSaleAsync(request.CarId, correlationId, bearerToken, ct);
        if (reservation is null)
        {
            saga.State = SagaStates.Failed;
            saga.StepDataJson = JsonSerializer.Serialize(new SagaStepData { CarId = request.CarId, LastError = reserveError });
            saga.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return (null, Results.BadRequest(reserveError));
        }

        var car = await catalog.GetCarAsync(request.CarId, ct);
        if (car is null)
        {
            await catalog.ReleaseReservationAsync(request.CarId, reservation.ReservationId, bearerToken, ct);
            saga.State = SagaStates.Failed;
            await db.SaveChangesAsync(ct);
            return (null, Results.BadRequest("Car not found in catalog."));
        }

        var sale = request.ToSale(car.SalePrice, ownerUsername);
        sale.ReservationId = reservation.ReservationId;
        sale.CorrelationId = correlationId;

        var outbox = new OutboxMessage
        {
            Type = "SaleCreated",
            Payload = JsonSerializer.Serialize(new SaleCreatedEvent(sale.CarId, sale.Id, reservation.ReservationId))
        };

        db.Sales.Add(sale);
        db.OutboxMessages.Add(outbox);

        saga.State = SagaStates.Persisted;
        saga.StepDataJson = JsonSerializer.Serialize(new SagaStepData
        {
            CarId = request.CarId,
            ReservationId = reservation.ReservationId,
            SaleId = sale.Id,
            OutboxMessageId = outbox.Id
        });
        saga.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return (sale, null);
    }

    public async Task CompleteAsync(string holderReference, Guid saleId, CancellationToken ct)
    {
        var saga = await db.SagaInstances.FirstOrDefaultAsync(
            x => x.CorrelationId == holderReference && x.Type == SagaTypes.CreateSale,
            ct);
        if (saga is null)
        {
            return;
        }

        var data = JsonSerializer.Deserialize<SagaStepData>(saga.StepDataJson) ?? new SagaStepData();
        if (data.SaleId != saleId)
        {
            return;
        }

        saga.State = SagaStates.Completed;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task CompensateAsync(Guid saleId, Guid reservationId, Guid carId, CancellationToken ct)
    {
        var sale = await db.Sales.FirstOrDefaultAsync(x => x.Id == saleId, ct);
        if (sale is not null && sale.Status != SaleStatuses.Cancelled)
        {
            sale.Status = SaleStatuses.Cancelled;
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "SaleCancelled",
                Payload = JsonSerializer.Serialize(new SaleCancelledEvent(carId, saleId, reservationId))
            });
        }

        var sagas = await db.SagaInstances.Where(x => x.Type == SagaTypes.CreateSale).ToListAsync(ct);
        foreach (var instance in sagas)
        {
            var data = JsonSerializer.Deserialize<SagaStepData>(instance.StepDataJson);
            if (data?.SaleId != saleId)
            {
                continue;
            }

            instance.State = SagaStates.Failed;
            instance.UpdatedAtUtc = DateTime.UtcNow;
        }

        await catalog.ReleaseReservationAsync(carId, reservationId, bearerToken: null, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ProcessTimeoutsAsync(TimeSpan publishedTimeout, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.SagaInstances
            .Where(x => x.Type == SagaTypes.CreateSale &&
                        (x.State == SagaStates.Persisted || x.State == SagaStates.Published))
            .ToListAsync(ct);

        foreach (var saga in candidates)
        {
            var data = JsonSerializer.Deserialize<SagaStepData>(saga.StepDataJson) ?? new SagaStepData();
            if (data.OutboxMessageId is Guid outboxId)
            {
                var outbox = await db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == outboxId, ct);
                if (outbox?.ProcessedOnUtc is not null && saga.State == SagaStates.Persisted)
                {
                    saga.State = SagaStates.Published;
                    saga.UpdatedAtUtc = now;
                    continue;
                }
            }

            if (saga.State == SagaStates.Published && now - saga.UpdatedAtUtc > publishedTimeout)
            {
                if (data.SaleId is null || data.ReservationId is null || data.CarId is null)
                {
                    continue;
                }

                saga.State = SagaStates.Compensating;
                saga.UpdatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await CompensateAsync(data.SaleId.Value, data.ReservationId.Value, data.CarId.Value, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
