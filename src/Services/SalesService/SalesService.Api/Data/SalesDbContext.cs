using Microsoft.EntityFrameworkCore;
using SalesService.Api.Models;

namespace SalesService.Api.Data;

public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<SaleOrder> Sales => Set<SaleOrder>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();
    public DbSet<SagaInstance> SagaInstances => Set<SagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaInstance>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<SagaInstance>().HasIndex(x => new { x.Type, x.State });
        modelBuilder.Entity<SagaInstance>().HasIndex(x => new { x.Type, x.EntityId });
        modelBuilder.Entity<SaleOrder>()
            .HasIndex(x => new { x.OwnerUsername, x.SoldAtUtc });

        modelBuilder.Entity<IdempotentRequest>()
            .HasIndex(x => new { x.KeyHash, x.Path })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}

public static class DataInitializationExtensions
{
    public static async Task InitializeSalesDataAsync(this IServiceProvider services, bool runMigrations)
    {
        if (!runMigrations)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        await db.Database.MigrateAsync();
    }
}
