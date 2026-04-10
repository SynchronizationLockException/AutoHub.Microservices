using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Models;

namespace NotificationService.Api.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedMessage>().HasIndex(x => x.MessageId).IsUnique();
        modelBuilder.Entity<NotificationDelivery>().HasIndex(x => x.DeliveredOnUtc);
    }
}

public static class NotificationDataInitializationExtensions
{
    public static async Task InitializeNotificationDataAsync(this IServiceProvider services, bool runMigrations)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        if (runMigrations)
        {
            await db.Database.MigrateAsync();
        }
    }
}
