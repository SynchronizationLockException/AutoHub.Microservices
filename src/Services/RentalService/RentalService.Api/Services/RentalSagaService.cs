using BuildingBlocks.Contracts;
using BuildingBlocks.Saga;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
using RentalService.Api.Models;
using System.Text.Json;

namespace RentalService.Api.Services;

public sealed class RentalSagaService(
    RentalDbContext db,
    CatalogReservationClient catalog)
{
    public async Task<(RentalContract? Rental, IResult? Error)> StartCreateRentalAsync(
        CreateRentalRequest request,
        string ownerUsername,
        string correlationId,
        string? bearerToken,
        CancellationToken ct)
    {
        var saga = new SagaInstance
        {
            CorrelationId = correlationId,
            Type = SagaTypes.CreateRental,
            State = SagaStates.Reserved,
            StepDataJson = JsonSerializer.Serialize(new SagaStepData { CarId = request.CarId })
        };
        db.SagaInstances.Add(saga);

        var (reservation, reserveError) = await catalog.ReserveRentAsync(request.CarId, correlationId, bearerToken, ct);
        if (reservation is null)
        {
            saga.State = SagaStates.Failed;
            saga.StepDataJson = JsonSerializer.Serialize(new SagaStepData
            {
                CarId = request.CarId,
                LastError = reserveError
            });
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

        var rental = request.ToRental(car.PricePerDay, ownerUsername);
        rental.ReservationId = reservation.ReservationId;
        rental.CorrelationId = correlationId;

        var outbox = new OutboxMessage
        {
            Type = "RentalCreated",
            Payload = JsonSerializer.Serialize(new RentalCreatedEvent(
                rental.CarId,
                rental.Id,
                reservation.ReservationId))
        };

        db.Rentals.Add(rental);
        db.OutboxMessages.Add(outbox);

        saga.State = SagaStates.Persisted;
        saga.StepDataJson = JsonSerializer.Serialize(new SagaStepData
        {
            CarId = request.CarId,
            ReservationId = reservation.ReservationId,
            RentalId = rental.Id,
            OutboxMessageId = outbox.Id
        });
        saga.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return (rental, null);
    }

    public async Task CompleteAsync(string holderReference, Guid rentalId, CancellationToken ct)
    {
        var saga = await db.SagaInstances.FirstOrDefaultAsync(
            x => x.CorrelationId == holderReference && x.Type == SagaTypes.CreateRental,
            ct);
        if (saga is null)
        {
            return;
        }

        var data = JsonSerializer.Deserialize<SagaStepData>(saga.StepDataJson) ?? new SagaStepData();
        if (data.RentalId != rentalId)
        {
            return;
        }

        saga.State = SagaStates.Completed;
        saga.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task CompensateAsync(
        Guid rentalId,
        Guid reservationId,
        Guid carId,
        string? bearerToken,
        CancellationToken ct)
    {
        var rental = await db.Rentals.FirstOrDefaultAsync(x => x.Id == rentalId, ct);
        if (rental is not null && rental.Status != RentalStatuses.Cancelled)
        {
            rental.Status = RentalStatuses.Cancelled;
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "RentalCancelled",
                Payload = JsonSerializer.Serialize(new RentalCancelledEvent(carId, rentalId, reservationId))
            });
        }

        var saga = await db.SagaInstances
            .Where(x => x.Type == SagaTypes.CreateRental)
            .ToListAsync(ct);
        foreach (var instance in saga)
        {
            var data = JsonSerializer.Deserialize<SagaStepData>(instance.StepDataJson);
            if (data?.RentalId != rentalId)
            {
                continue;
            }

            instance.State = SagaStates.Failed;
            instance.UpdatedAtUtc = DateTime.UtcNow;
        }

        await catalog.ReleaseReservationAsync(carId, reservationId, bearerToken, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ProcessTimeoutsAsync(TimeSpan publishedTimeout, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.SagaInstances
            .Where(x => x.Type == SagaTypes.CreateRental &&
                        (x.State == SagaStates.Persisted || x.State == SagaStates.Published))
            .ToListAsync(ct);

        foreach (var saga in candidates)
        {
            var data = JsonSerializer.Deserialize<SagaStepData>(saga.StepDataJson) ?? new SagaStepData();
            if (data.OutboxMessageId is Guid outboxId)
            {
                var outbox = await db.OutboxMessages.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == outboxId, ct);
                if (outbox?.ProcessedOnUtc is not null && saga.State == SagaStates.Persisted)
                {
                    saga.State = SagaStates.Published;
                    saga.UpdatedAtUtc = now;
                    continue;
                }
            }

            if (saga.State == SagaStates.Published && now - saga.UpdatedAtUtc > publishedTimeout)
            {
                if (data.RentalId is null || data.ReservationId is null || data.CarId is null)
                {
                    continue;
                }

                saga.State = SagaStates.Compensating;
                saga.UpdatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await CompensateAsync(data.RentalId.Value, data.ReservationId.Value, data.CarId.Value, bearerToken: null, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
