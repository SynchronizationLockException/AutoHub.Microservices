using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Models;

namespace PaymentService.Api.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>()
            .HasIndex(x => new { x.ReferenceKind, x.ReferenceId })
            .IsUnique();
        modelBuilder.Entity<Payment>()
            .HasIndex(x => new { x.OwnerUsername, x.CreatedAtUtc });

        modelBuilder.Entity<IdempotentRequest>()
            .HasIndex(x => new { x.KeyHash, x.Path })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}

public static class DataInitializationExtensions
{
    public static async Task InitializePaymentDataAsync(this IServiceProvider services, bool runMigrations)
    {
        if (!runMigrations)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();
    }
}
