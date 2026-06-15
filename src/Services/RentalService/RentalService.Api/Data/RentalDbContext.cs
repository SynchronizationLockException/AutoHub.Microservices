using Microsoft.EntityFrameworkCore;
using RentalService.Api.Models;

namespace RentalService.Api.Data;

public sealed class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options)
{
    public DbSet<RentalContract> Rentals => Set<RentalContract>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();
    public DbSet<SagaInstance> SagaInstances => Set<SagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaInstance>()
            .HasIndex(x => x.CorrelationId);

        modelBuilder.Entity<SagaInstance>()
            .HasIndex(x => new { x.Type, x.State });

        modelBuilder.Entity<SagaInstance>()
            .HasIndex(x => new { x.Type, x.EntityId });
        modelBuilder.Entity<RentalContract>()
            .HasIndex(x => new { x.OwnerUsername, x.CreatedAtUtc });

        modelBuilder.Entity<IdempotentRequest>()
            .HasIndex(x => new { x.KeyHash, x.Path })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}

public static class DataInitializationExtensions
{
    public static async Task InitializeRentalDataAsync(this IServiceProvider services, bool runMigrations)
    {
        if (!runMigrations)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentalDbContext>();
        await db.Database.MigrateAsync();
    }
}
