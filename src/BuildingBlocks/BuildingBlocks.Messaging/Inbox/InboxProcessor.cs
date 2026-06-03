using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BuildingBlocks.Messaging.Inbox;

public static class InboxProcessor
{
    public static async Task<bool> TryProcessAsync<TDbContext, TProcessed>(
        TDbContext db,
        string messageId,
        Func<CancellationToken, Task> handler,
        Func<TProcessed> createProcessed,
        CancellationToken cancellationToken)
        where TDbContext : DbContext
        where TProcessed : class, IProcessedMessage
    {
        using var activity = MessagingActivitySource.Instance.StartActivity("inbox.process");
        activity?.SetTag("messaging.message_id", messageId);

        var alreadyProcessed = await db.Set<TProcessed>()
            .AnyAsync(x => x.MessageId == messageId, cancellationToken);
        if (alreadyProcessed)
        {
            return false;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await handler(cancellationToken);
            db.Set<TProcessed>().Add(createProcessed());
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
