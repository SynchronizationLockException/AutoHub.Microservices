using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CarCatalogService.Api.Services;

public sealed class ReservationService(CarCatalogDbContext db)
{
    private const int DefaultTtlMinutes = 15;
    private const int MaxTtlMinutes = 60;

    public async Task<(ReservationResponse? Success, string? Error)> TryReserveAsync(
        Guid carId,
        CreateReservationRequest request,
        CancellationToken ct)
    {
        if (request.TtlMinutes is < 1 or > MaxTtlMinutes)
        {
            return (null, $"TtlMinutes must be between 1 and {MaxTtlMinutes}.");
        }

        var purpose = request.Purpose.Trim();
        if (purpose is not (ReservationPurposes.Rent or ReservationPurposes.Sale))
        {
            return (null, "Purpose must be Rent or Sale.");
        }

        var holder = request.HolderReference.Trim();
        if (string.IsNullOrEmpty(holder))
        {
            return (null, "HolderReference is required.");
        }

        var ttlMinutes = request.TtlMinutes ?? DefaultTtlMinutes;
        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == carId, ct);

            if (car is null)
            {
                await tx.RollbackAsync(ct);
                return (null, "Car not found.");
            }

            var hasActive = await db.Reservations.AnyAsync(
                x => x.CarId == carId &&
                     x.Status == ReservationStatuses.Active &&
                     x.ExpiresAtUtc > now,
                ct);

            if (hasActive)
            {
                await tx.RollbackAsync(ct);
                return (null, "Car already has an active reservation.");
            }

            if (purpose == ReservationPurposes.Rent && !car.IsAvailableForRent)
            {
                await tx.RollbackAsync(ct);
                return (null, "Car is not available for rent.");
            }

            if (purpose == ReservationPurposes.Sale && !car.IsAvailableForSale)
            {
                await tx.RollbackAsync(ct);
                return (null, "Car is not available for sale.");
            }

            var reservation = new CarReservation
            {
                CarId = carId,
                Purpose = purpose,
                HolderReference = holder,
                ExpiresAtUtc = now.AddMinutes(ttlMinutes)
            };

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return (new ReservationResponse(
                reservation.Id,
                reservation.CarId,
                reservation.Purpose,
                reservation.Status,
                reservation.HolderReference,
                reservation.ExpiresAtUtc), null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return (null, "Concurrent update detected. Retry reservation.");
        }
    }

    public async Task<(ReservationResponse? Success, string? Error)> TryReleaseAsync(
        Guid carId,
        Guid reservationId,
        CancellationToken ct)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(
            x => x.Id == reservationId && x.CarId == carId,
            ct);
        if (reservation is null)
        {
            return (null, "Reservation not found.");
        }

        if (reservation.Status is ReservationStatuses.Released or ReservationStatuses.Expired)
        {
            return (Map(reservation), null);
        }

        reservation.Status = ReservationStatuses.Released;
        await RestoreAvailabilityIfPossibleAsync(reservation.CarId, ct);
        await db.SaveChangesAsync(ct);
        return (Map(reservation), null);
    }

    public async Task<(ReservationResponse? Success, string? Error)> TryConfirmAsync(
        Guid carId,
        Guid reservationId,
        CancellationToken ct)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(
            x => x.Id == reservationId && x.CarId == carId,
            ct);
        if (reservation is null)
        {
            return (null, "Reservation not found.");
        }

        if (reservation.Status == ReservationStatuses.Confirmed)
        {
            return (Map(reservation), null);
        }

        if (reservation.Status != ReservationStatuses.Active || reservation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return (null, "Reservation is not active.");
        }

        var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == carId, ct);
        if (car is null)
        {
            return (null, "Car not found.");
        }

        reservation.Status = ReservationStatuses.Confirmed;
        if (reservation.Purpose == ReservationPurposes.Rent)
        {
            car.IsAvailableForRent = false;
            car.IsAvailableForSale = false;
        }
        else
        {
            car.IsAvailableForSale = false;
            car.IsAvailableForRent = false;
        }

        await db.SaveChangesAsync(ct);
        return (Map(reservation), null);
    }

    public async Task ReleaseAvailabilityForCarAsync(Guid carId, CancellationToken ct)
    {
        await RestoreAvailabilityIfPossibleAsync(carId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task RestoreAvailabilityIfPossibleAsync(Guid carId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var blocking = await db.Reservations.AnyAsync(
            x => x.CarId == carId &&
                 (x.Status == ReservationStatuses.Confirmed ||
                  (x.Status == ReservationStatuses.Active && x.ExpiresAtUtc > now)),
            ct);
        if (blocking)
        {
            return;
        }

        var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == carId, ct);
        if (car is null)
        {
            return;
        }

        car.IsAvailableForRent = true;
        car.IsAvailableForSale = true;
    }

    public async Task ExpireStaleReservationsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stale = await db.Reservations
            .Where(x => x.Status == ReservationStatuses.Active && x.ExpiresAtUtc <= now)
            .ToListAsync(ct);

        foreach (var reservation in stale)
        {
            reservation.Status = ReservationStatuses.Expired;
            await RestoreAvailabilityIfPossibleAsync(reservation.CarId, ct);
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static ReservationResponse Map(CarReservation reservation) =>
        new(
            reservation.Id,
            reservation.CarId,
            reservation.Purpose,
            reservation.Status,
            reservation.HolderReference,
            reservation.ExpiresAtUtc);
}
