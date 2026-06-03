using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarCatalogService.Api.Endpoints;

public static class CatalogEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cars", async (int? page, int? pageSize, CarCatalogDbContext db, CancellationToken ct) =>
        {
            var currentPage = page.GetValueOrDefault(1);
            var currentPageSize = pageSize.GetValueOrDefault(DefaultPageSize);
            if (currentPage <= 0 || currentPageSize <= 0)
            {
                return Results.BadRequest("Query params page and pageSize must be positive integers.");
            }

            currentPageSize = Math.Min(currentPageSize, MaxPageSize);   
            var skip = (currentPage - 1) * currentPageSize;
            var cars = await db.Cars
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync(ct);

            return Results.Ok(cars);
        });

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
