using AuthService.Api.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthService.Api.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AuthUser> Users => Set<AuthUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
    }
}

public static class AuthDataInitializationExtensions
{
    public static async Task InitializeAuthDataAsync(this IServiceProvider services, bool runMigrations)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AuthDataInitialization");

        if (runMigrations)
        {
            await db.Database.MigrateAsync();
        }

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var shouldSeedDemoUsers = configuration.GetValue<bool>("AuthSeed:Enabled");
        if (!shouldSeedDemoUsers)
        {
            logger.LogInformation("Auth demo user seeding is disabled.");
            return;
        }

        var clientPassword = configuration["AuthSeed:ClientPassword"];
        var managerPassword = configuration["AuthSeed:ManagerPassword"];
        var adminPassword = configuration["AuthSeed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(clientPassword) ||
            string.IsNullOrWhiteSpace(managerPassword) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Auth demo user seeding skipped. Set AuthSeed passwords in configuration.");
            return;
        }

        db.Users.AddRange(
            new AuthUser { Username = "client", PasswordHash = BCrypt.Net.BCrypt.HashPassword(clientPassword), Role = "Client" },
            new AuthUser { Username = "manager", PasswordHash = BCrypt.Net.BCrypt.HashPassword(managerPassword), Role = "Manager" },
            new AuthUser { Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword), Role = "Admin" });
        await db.SaveChangesAsync();
    }
}
