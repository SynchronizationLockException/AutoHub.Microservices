using CarCatalogService.Api.Data;
using CarCatalogService.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarCatalogService.Api.Endpoints;

public static class CatalogEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public static void MapCatalogEndpoints(this WebApplication app)
    {
        MapCarRoutes(app, "/api/cars");
        MapCarRoutes(app, "/api/v1/cars");
    }

    private static void MapCarRoutes(WebApplication app, string prefix)
    {
        app.MapGet(prefix, async (int? page, int? pageSize, CarCatalogDbContext db, IMemoryCache cache, CancellationToken ct) =>
        {
            var currentPage = page.GetValueOrDefault(1);
            var currentPageSize = pageSize.GetValueOrDefault(DefaultPageSize);
            if (currentPage <= 0 || currentPageSize <= 0)
            {
                return Results.BadRequest("Query params page and pageSize must be positive integers.");
            }

            currentPageSize = Math.Min(currentPageSize, MaxPageSize);
            var cacheKey = $"cars:{currentPage}:{currentPageSize}";
            if (cache.TryGetValue(cacheKey, out List<Car>? cached))
            {
                return Results.Ok(cached);
            }

            var skip = (currentPage - 1) * currentPageSize;
            var cars = await db.Cars
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync(ct);

            cache.Set(cacheKey, cars, CacheDuration);
            return Results.Ok(cars);
        });

        app.MapGet($"{prefix}/{{id:guid}}", async (Guid id, CarCatalogDbContext db, IMemoryCache cache, CancellationToken ct) =>
        {
            var cacheKey = $"car:{id}";
            if (cache.TryGetValue(cacheKey, out Car? cached))
            {
                return Results.Ok(cached);
            }

            var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (car is null)
            {
                return Results.NotFound();
            }

            cache.Set(cacheKey, car, CacheDuration);
            return Results.Ok(car);
        });

        app.MapPost(prefix, async (CreateCarRequest request, CarCatalogDbContext db, IMemoryCache cache, CancellationToken ct) =>
        {
            var car = request.ToCar();
            db.Cars.Add(car);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/cars/{car.Id}", car);
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        app.MapPatch($"{prefix}/{{id:guid}}/availability", async (
            Guid id,
            UpdateAvailabilityRequest request,
            CarCatalogDbContext db,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var car = await db.Cars.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (car is null)
            {
                return Results.NotFound();
            }

            car.IsAvailableForRent = request.IsAvailableForRent;
            car.IsAvailableForSale = request.IsAvailableForSale;
            await db.SaveChangesAsync(ct);
            cache.Remove($"car:{id}");
            return Results.Ok(car);
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
    }
}
