using Microsoft.EntityFrameworkCore;

namespace Workflow.Infrastructure;

// A claim is ownership of a delivery attempt, not proof of exactly-once external execution.
public static class OutboxLease
{
    public static IQueryable<Outbox> Due(WorkflowDb db, DateTime now) => db.Outbox.Where(o =>
        (o.Status == "pending" && o.NextAttempt <= now) ||
        (o.Status == "processing" && o.LeaseUntil != null && o.LeaseUntil <= now));

    public static async Task<Outbox?> Claim(WorkflowDb db, long id, TimeSpan duration, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var row = await Due(db, now).AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, token);
        if (row == null) return null;
        var claim = Guid.NewGuid().ToString("N");
        var until = now.Add(duration);
        var changed = await Due(db, now).Where(o => o.Id == id && o.Version == row.Version)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Status, "processing")
                .SetProperty(o => o.ClaimId, claim).SetProperty(o => o.ClaimedAt, now)
                .SetProperty(o => o.LeaseUntil, until).SetProperty(o => o.Version, o => o.Version + 1), token);
        return changed == 1 ? await db.Outbox.SingleAsync(o => o.Id == id && o.ClaimId == claim, token) : null;
    }

    private static IQueryable<Outbox> Owned(WorkflowDb db, long id, string claim, DateTime now) =>
        db.Outbox.Where(o => o.Id == id && o.Status == "processing" && o.ClaimId == claim && o.LeaseUntil > now);

    public static async Task<bool> Renew(WorkflowDb db, long id, string claim, TimeSpan duration, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        return await Owned(db, id, claim, now).ExecuteUpdateAsync(set =>
            set.SetProperty(o => o.LeaseUntil, now.Add(duration)).SetProperty(o => o.Version, o => o.Version + 1), token) == 1;
    }

    public static async Task<bool> Complete(WorkflowDb db, Outbox message, string claim, CancellationToken token)
    {
        // Fence acknowledgement and workflow/history/result changes in the same transaction.
        // No transaction is held open while calling the target.
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var changed = await Owned(db, message.Id, claim, DateTime.UtcNow).ExecuteUpdateAsync(set =>
            set.SetProperty(o => o.Status, "sent").SetProperty(o => o.Message, message.Message)
                .SetProperty(o => o.ClaimId, (string?)null).SetProperty(o => o.LeaseUntil, (DateTime?)null)
                .SetProperty(o => o.Version, o => o.Version + 1), token);
        if (changed != 1) return false;
        db.Entry(message).State = EntityState.Detached; // Heartbeat updated Version in its own context.
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    public static async Task<bool> AwaitResult(WorkflowDb db, Outbox message, string claim, TimeSpan recoveryInterval, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        // A callback that already completed this operation wins: never put sent back into waiting.
        var changed = await Owned(db, message.Id, claim, now).ExecuteUpdateAsync(set =>
            set.SetProperty(o => o.Status, "awaitingResult").SetProperty(o => o.Message, message.Message)
                .SetProperty(o => o.NextAttempt, now.Add(recoveryInterval))
                .SetProperty(o => o.ClaimId, (string?)null).SetProperty(o => o.LeaseUntil, (DateTime?)null)
                .SetProperty(o => o.Version, o => o.Version + 1), token);
        db.Entry(message).State = EntityState.Detached;
        return changed == 1;
    }

    public static async Task<bool> Retry(WorkflowDb db, long id, string claim, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var row = await Owned(db, id, claim, now).AsNoTracking().SingleOrDefaultAsync(token);
        if (row == null) return false;
        var attempts = row.Attempts + 1;
        var status = row.Kind == "businessAction" && attempts >= 5 ? "blocked" : "pending";
        var next = now.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(attempts, 10)) * 5));
        return await Owned(db, id, claim, now).Where(o => o.Version == row.Version).ExecuteUpdateAsync(set =>
            set.SetProperty(o => o.Status, status).SetProperty(o => o.Attempts, attempts)
                .SetProperty(o => o.NextAttempt, next).SetProperty(o => o.ClaimId, (string?)null)
                .SetProperty(o => o.LeaseUntil, (DateTime?)null).SetProperty(o => o.Version, o => o.Version + 1), token) == 1;
    }
}
