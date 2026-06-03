using BuildingBlocks.Hosting;
using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using CarCatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarCatalogService.Api.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cars/{carId:guid}/reservations", async (
            Guid carId,
            CreateReservationRequest request,
            ReservationService reservations,
            CancellationToken ct) =>
        {
            var (success, error) = await reservations.TryReserveAsync(carId, request, ct);
            if (success is null)
            {
                return Results.BadRequest(error);
            }

            return Results.Created($"/api/cars/{carId}/reservations/{success.ReservationId}", success);
        });

        app.MapGet("/api/cars/{carId:guid}/reservations/{reservationId:guid}", async (
            Guid carId,
            Guid reservationId,
            CarCatalogDbContext db,
            CancellationToken ct) =>
        {
            var reservation = await db.Reservations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == reservationId && x.CarId == carId, ct);
            return reservation is null
                ? Results.NotFound()
                : Results.Ok(new ReservationResponse(
                    reservation.Id,
                    reservation.CarId,
                    reservation.Purpose,
                    reservation.Status,
                    reservation.HolderReference,
                    reservation.ExpiresAtUtc));
        });

        app.MapDelete("/api/cars/{carId:guid}/reservations/{reservationId:guid}", async (
            Guid carId,
            Guid reservationId,
            ReservationService reservations,
            CancellationToken ct) =>
        {
            var (success, error) = await reservations.TryReleaseAsync(carId, reservationId, ct);
            if (success is null)
            {
                return Results.NotFound(error);
            }

            return Results.Ok(success);
        });

        app.MapPost("/api/cars/{carId:guid}/reservations/{reservationId:guid}/confirm", async (
            Guid carId,
            Guid reservationId,
            HttpContext httpContext,
            IConfiguration configuration,
            ReservationService reservations,
            CancellationToken ct) =>
        {
            if (!httpContext.IsValidInternalRequest(configuration))
            {
                return Results.Unauthorized();
            }

            var (success, error) = await reservations.TryConfirmAsync(carId, reservationId, ct);
            if (success is null)
            {
                return Results.BadRequest(error);
            }

            return Results.Ok(success);
        });

        app.MapDelete("/api/internal/cars/{carId:guid}/reservations/{reservationId:guid}", async (
            Guid carId,
            Guid reservationId,
            HttpContext httpContext,
            IConfiguration configuration,
            ReservationService reservations,
            CancellationToken ct) =>
        {
            if (!httpContext.IsValidInternalRequest(configuration))
            {
                return Results.Unauthorized();
            }

            var (success, error) = await reservations.TryReleaseAsync(carId, reservationId, ct);
            if (success is null)
            {
                return Results.NotFound(error);
            }

            return Results.Ok(success);
        });
    }
}
