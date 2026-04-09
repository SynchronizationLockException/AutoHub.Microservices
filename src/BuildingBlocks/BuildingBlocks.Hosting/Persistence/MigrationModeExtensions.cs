namespace BuildingBlocks.Hosting.Persistence;

public static class MigrationModeExtensions
{
    public static bool IsMigrationMode(this string[] args)
        => args.Any(x => string.Equals(x, "--migrate", StringComparison.OrdinalIgnoreCase));
}
