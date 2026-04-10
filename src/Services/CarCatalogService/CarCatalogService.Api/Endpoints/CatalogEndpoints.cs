using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarCatalogService.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cars", async (CarCatalogDbContext db, CancellationToken ct) =>
            await db.Cars.AsNoTracking().ToListAsync(ct));

        app.MapGet("/api/cars/{id:guid}", async (Guid id, CarCatalogDbContext db, CancellationToken ct) =>
        {
            var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return car is null ? Results.NotFound() : Results.Ok(car);
        });

        app.MapPost("/api/cars", async (CreateCarRequest request, CarCatalogDbContext db, CancellationToken ct) =>
        {
            var car = request.ToCar();
            db.Cars.Add(car);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/cars/{car.Id}", car);
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        app.MapPatch("/api/cars/{id:guid}/availability", async (Guid id, UpdateAvailabilityRequest request, CarCatalogDbContext db, CancellationToken ct) =>
        {
            var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (car is null)
            {
                return Results.NotFound();
            }

            car.IsAvailableForRent = request.IsAvailableForRent;
            car.IsAvailableForSale = request.IsAvailableForSale;
            await db.SaveChangesAsync(ct);
            return Results.Ok(car);
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
    }
}
