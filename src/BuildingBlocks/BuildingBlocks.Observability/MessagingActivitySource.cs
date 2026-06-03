using System.Diagnostics;

namespace BuildingBlocks.Observability;

public static class MessagingActivitySource
{
    public static readonly ActivitySource Instance = new("BuildingBlocks.Messaging");
}
