using Microsoft.EntityFrameworkCore;
using Workflow.Infrastructure;

namespace Workflow;

public sealed class OutboxOptions
{
    public int BusinessConcurrency { get; init; } = 5;
    public int EventConcurrency { get; init; } = 5;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(60);
}

public class NotificationWorker(IServiceScopeFactory scopes, IMailSender mail, ILogger<NotificationWorker> logger,
    OutboxOptions options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.BusinessConcurrency < 1 || options.EventConcurrency < 1 || options.PollInterval <= TimeSpan.Zero ||
            options.LeaseDuration < TimeSpan.FromMilliseconds(30) || options.AttemptTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Invalid Outbox worker limits");
        // Separate capacities: a full business lane cannot occupy the notification/mail lane.
        return Task.WhenAll(Lane(true, options.BusinessConcurrency, stoppingToken),
            Lane(false, options.EventConcurrency, stoppingToken));
    }

    private async Task Lane(bool business, int concurrency, CancellationToken stoppingToken)
    {
        // Each slot polls independently, so a slow item does not hold up the next batch.
        await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Slot(business, stoppingToken)));
    }

    private async Task Slot(bool business, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkflowDb>();
                var id = await OutboxLease.Due(db, DateTime.UtcNow).Where(o => business ? o.Kind == "businessAction" : o.Kind != "businessAction")
                    .OrderBy(o => o.Id).Select(o => (long?)o.Id).FirstOrDefaultAsync(stoppingToken);
                if (id != null && await OutboxLease.Claim(db, id.Value, options.LeaseDuration, stoppingToken) is { } message)
                {
                    await Deliver(scope.ServiceProvider, db, message, stoppingToken);
                    continue;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(ex, "Outbox {Lane} polling failed", business ? "business" : "event"); }
            await Task.Delay(options.PollInterval, stoppingToken);
        }
    }

    private async Task Deliver(IServiceProvider services, WorkflowDb db, Outbox message, CancellationToken stoppingToken)
    {
        var claim = message.ClaimId!;
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        attempt.CancelAfter(options.AttemptTimeout);
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = Heartbeat(message.Id, claim, attempt, heartbeatStop.Token);
        Exception? failure = null;
        var awaitingResult = false;
        try
        {
            if (message.Kind == "businessAction")
            {
                var actions = services.GetRequiredService<BusinessActions>();
                if (Json.Read<BusinessJob>(message.Message).Dispatch != null)
                    awaitingResult = await actions.Publish(db, message, services.GetRequiredService<IBusinessPublisher>(), attempt.Token);
                else await actions.Deliver(db, message, attempt.Token);
            }
            else if (message.Kind == "mail")
            {
                if (!string.IsNullOrWhiteSpace(message.Recipient))
                    await mail.SendAsync(message.Recipient, "עדכון בפנייה", message.Message, attempt.Token);
            }
            else if (!await db.Notifications.AnyAsync(n => n.OutboxId == message.Id, attempt.Token))
                db.Notifications.Add(new Notification { OutboxId = message.Id, CaseId = message.CaseId, Message = message.Message });
            attempt.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            // Stop and join heartbeat before acknowledgement/retry; never share a DbContext.
            heartbeatStop.Cancel();
            await heartbeat;
        }
        if (stoppingToken.IsCancellationRequested) return; // Leave lease for recovery after shutdown/crash.
        if (attempt.IsCancellationRequested && failure == null) failure = new OperationCanceledException("Outbox attempt cancelled");
        try
        {
            if (failure == null)
            {
                var acknowledged = awaitingResult
                    ? await OutboxLease.AwaitResult(db, message, claim, services.GetRequiredService<BusinessDispatch>().RecoveryInterval, stoppingToken)
                    : await OutboxLease.Complete(db, message, claim, stoppingToken);
                if (acknowledged) return;
            }
        }
        catch (Exception ex) { failure = ex; }
        db.ChangeTracker.Clear();
        if (failure != null) logger.LogWarning(failure, "Outbox item {Id} will be retried if still owned", message.Id);
        await OutboxLease.Retry(db, message.Id, claim, stoppingToken);
    }

    private async Task Heartbeat(long id, string claim, CancellationTokenSource attempt, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromTicks(options.LeaseDuration.Ticks / 3));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkflowDb>();
                if (!await OutboxLease.Renew(db, id, claim, options.LeaseDuration, token))
                { attempt.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Outbox item {Id} lease renewal failed", id);
            attempt.Cancel(); // Fail closed: a worker without proven ownership cannot acknowledge.
        }
    }
}
