using CarCatalogService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarCatalogService.Api.Data;

public sealed class CarCatalogDbContext(DbContextOptions<CarCatalogDbContext> options) : DbContext(options)
{
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedMessage>().HasIndex(x => x.MessageId).IsUnique();
    }
}

public static class DataInitializationExtensions
{
    public static async Task InitializeCatalogDataAsync(this IServiceProvider services, bool runMigrations)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarCatalogDbContext>();
        if (runMigrations)
        {
            await db.Database.MigrateAsync();
        }

        if (await db.Cars.AnyAsync())
        {
            return;
        }

        db.Cars.AddRange(
            new Car { Vin = "WAUZZZ8V4KA123456", Brand = "Audi", Model = "A3", Year = 2022, PricePerDay = 70, SalePrice = 25000 },
            new Car { Vin = "WVWZZZAUZLW987654", Brand = "Volkswagen", Model = "Golf", Year = 2021, PricePerDay = 60, SalePrice = 22000 });

        await db.SaveChangesAsync();
    }
}
