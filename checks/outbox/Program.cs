using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workflow;
using Workflow.Infrastructure;

// Real dispatcher + real SQLite contexts. Test-only gates/counters, no product endpoints or engine bypass.
var folder = Path.Combine(Path.GetTempPath(), "workflow-outbox-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var keyDirectory = new DirectoryInfo(Path.Combine(folder, "callback-keys"));
IDataProtector Protector() => DataProtectionProvider.Create(keyDirectory, builder =>
{
    builder.SetApplicationName("Workflow.BusinessCallbacks");
    if (OperatingSystem.IsWindows()) builder.ProtectKeysWithDpapi();
}).CreateProtector("BusinessCallbacks.v1");
var asyncOptions = new BusinessQueueOptions { ActionKeys = ["testAction"], TargetIds = new() { ["testAction"] = "test-target" }, CallbackUrl = "http://127.0.0.1:5081/integrations/business-actions/result" };
var dispatch = new BusinessDispatch(asyncOptions, Protector());
var delivery = dispatch.Create("testAction")!;
var tokenJob = new BusinessJob(new("testAction", new(), "ok", "fail"), "reviewer", "approve", "review", "waiting", 1, new() { ["value"] = "x" }, Dispatch: delivery);
var tokenMessage = new Outbox { Id = 123, Kind = "businessAction", Message = Json.Write(tokenJob) };
var firstCommand = dispatch.Command(tokenMessage);
var restartedDispatch = new BusinessDispatch(asyncOptions, Protector());
if (restartedDispatch.Command(tokenMessage).CallbackToken != firstCommand.CallbackToken ||
    firstCommand.Request.OperationId != "123" || !BusinessDispatch.ValidToken(delivery, firstCommand.CallbackToken) ||
    BusinessDispatch.ValidToken(delivery, "wrong") || tokenMessage.Message.Contains(firstCommand.CallbackToken))
    throw new Exception("Token persistence/integrity failed");
Console.WriteLine("PASS dispatch: protected token survives recreated provider; valid/wrong token checks; stable OperationId; no plaintext in stored job");
if (dispatch.Create("legacyAction") != null || new BusinessDispatch(asyncOptions, Protector()).Create("testAction")!.TokenHash == delivery.TokenHash)
    throw new Exception("Dispatch key selection or token uniqueness failed");
Console.WriteLine("PASS dispatch: per-action generic selection and distinct tokens");
var options = new DbContextOptionsBuilder<WorkflowDb>().UseSqlite("Data Source=" + Path.Combine(folder, "checks.db")).Options;
WorkflowDb Db() => new(options);
var token = CancellationToken.None;
var lease = TimeSpan.FromMilliseconds(350);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
async Task Wait(Func<Task<bool>> predicate, string message)
{
    var until = DateTime.UtcNow.AddSeconds(15);
    while (DateTime.UtcNow < until) { if (await predicate()) return; await Task.Delay(20); }
    throw new Exception("Timeout: " + message);
}
async Task<long> Row(string kind = "notification")
{
    await using var db = Db(); var row = new Outbox { CaseId = 1, Kind = kind, Recipient = "test@example.invalid", Message = "test" };
    db.Outbox.Add(row); await db.SaveChangesAsync(); return row.Id;
}
await using (var db = Db()) { await db.Database.EnsureCreatedAsync(); await Seed.Initialize(db); }

// C: two actual contexts race for one row.
var id = await Row();
async Task<Outbox?> RaceClaim() { await using var db = Db(); return await OutboxLease.Claim(db, id, lease, token); }
var claims = await Task.WhenAll(Task.Run(RaceClaim), Task.Run(RaceClaim));
Check(claims.Count(c => c != null) == 1, "C: competing workers obtain exactly one valid claim");
var old = claims.Single(c => c != null)!;

// D/E: abandon a claimed row (the durable effect of a crash); do not renew it.
await Task.Delay(450);
await using (var db = Db())
{
    var current = await OutboxLease.Claim(db, id, TimeSpan.FromSeconds(5), token);
    Check(current != null && current.ClaimId != old.ClaimId, "D: abandoned expired lease is reclaimable");
    db.Notifications.Add(new Notification { OutboxId = id, CaseId = 1, Message = "stale" });
    Check(!await OutboxLease.Complete(db, old, old.ClaimId!, token), "E: stale owner acknowledgement rejected");
    db.ChangeTracker.Clear();
    Check(!await db.Notifications.AnyAsync(n => n.OutboxId == id), "E: stale owner's staged effects were not saved");
    db.Notifications.Add(new Notification { OutboxId = id, CaseId = 1, Message = "recovered" });
    Check(await OutboxLease.Complete(db, current!, current!.ClaimId!, token), "D: reclaimed row completes");
    Check(!await OutboxLease.Complete(db, current, current.ClaimId!, token) && await db.Notifications.CountAsync(n => n.OutboxId == id) == 1,
        "F: duplicate acknowledgement cannot duplicate notification");
}

// F: retry formula and business blocking, using only the isolated fixture DB to advance due times.
id = await Row("businessAction");
for (var attempt = 1; attempt <= 5; attempt++)
{
    await using var db = Db();
    var row = (await OutboxLease.Claim(db, id, TimeSpan.FromSeconds(5), token))!;
    var before = DateTime.UtcNow;
    Check(await OutboxLease.Retry(db, id, row.ClaimId!, token), "F: retry owned attempt " + attempt);
    db.ChangeTracker.Clear(); var saved = await db.Outbox.SingleAsync(o => o.Id == id);
    var seconds = Math.Pow(2, attempt) * 5;
    Check(saved.Attempts == attempt && saved.NextAttempt >= before.AddSeconds(seconds) &&
        saved.NextAttempt <= DateTime.UtcNow.AddSeconds(seconds) && saved.Status == (attempt == 5 ? "blocked" : "pending"),
        "F: exponential backoff / blocking attempt " + attempt);
    if (attempt < 5) { saved.NextAttempt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
}

// Real generic business workflow used by dispatcher. Every command has a separate case/version.
var spec = new BusinessActionSpec("testAction", new() { ["value"] = "value" }, "complete", "fail");
var definition = new Definition("Outbox check", "review", [],
    [new("review", "Review", false), new("waiting", "Waiting", false), new("done", "Done", true), new("failed", "Failed", true)],
    [new("approve", "Approve", "review", "waiting", ["Reviewer"], "none", [], BusinessAction: spec),
     new("complete", "Complete", "waiting", "done", ["Admin"], "none", [], Trigger: "businessSuccess"),
     new("fail", "Fail", "waiting", "failed", ["Admin"], "none", [], Trigger: "businessFailure")], []);
int processId;
await using (var db = Db()) { var p = new ProcessVersion { Key = "outbox-check", DefinitionJson = Json.Write(definition) }; db.Processes.Add(p); await db.SaveChangesAsync(); processId = p.Id; }
async Task<long> Command()
{
    await using var db = Db();
    var item = new Case { ProviderId = 1, ProcessVersionId = processId, State = "waiting", Title = "test", Unit = "care" };
    db.Cases.Add(item); await db.SaveChangesAsync();
    var job = new BusinessJob(spec, "reviewer", "approve", "review", "waiting", 1, new() { ["value"] = "x" }, WaitingVersion: item.Version);
    var row = new Outbox { CaseId = item.Id, Kind = "businessAction", Message = Json.Write(job) }; db.Outbox.Add(row); await db.SaveChangesAsync(); return row.Id;
}
var business = new Gate(); var mail = new TestMail();
var handler = new BusinessHandler("testAction", "Test", ["value"], async (_, ct) =>
{ await business.Run(ct); return new(true, "completed"); }, (_, _) => Task.FromResult<BusinessResult?>(null));
var actions = new BusinessActions([handler]);
// Stage 2: result-only completion shares the real engine path, without invoking the target.
foreach (var success in new[] { true, false })
{
    var completionId = await Command();
    await using var db = Db();
    var row = (await OutboxLease.Claim(db, completionId, TimeSpan.FromSeconds(5), token))!;
    if (success)
    {
        row.Message = Json.Write(Json.Read<BusinessJob>(row.Message) with { Dispatch = dispatch.Create("testAction") });
        try { await actions.Deliver(db, row, token); throw new Exception("Async action reached synchronous Run"); }
        catch (RuleException ex) { Check(ex.Status == 409 && business.Max == 0, "async job cannot invoke synchronous target Run"); }
    }
    await BusinessActions.Complete(db, row, new(success, "reported result"));
    Check(await OutboxLease.Complete(db, row, row.ClaimId!, token), "result-only completion acknowledged");
    Check(!await OutboxLease.AwaitResult(db, row, row.ClaimId!, TimeSpan.FromMinutes(1), token),
        "late publisher acknowledgement cannot revert completed operation to waiting");
    db.ChangeTracker.Clear();
    var item = await db.Cases.SingleAsync(c => c.Id == row.CaseId);
    Check(item.State == (success ? "done" : "failed") &&
        await db.History.CountAsync(h => h.CaseId == row.CaseId) == 1 && business.Max == 0,
        "shared completion: configured " + (success ? "success" : "failure") + " without target Run");
    try { await BusinessActions.Complete(db, row, new(success, "reported result")); throw new Exception("Expected stale 409"); }
    catch (RuleException ex) { Check(ex.Status == 409, "shared completion rejects already advanced state"); }
}
id = await Command();
await using (var db = Db())
{
    var row = (await OutboxLease.Claim(db, id, TimeSpan.FromSeconds(5), token))!;
    row.Message = Json.Write(Json.Read<BusinessJob>(row.Message) with { Dispatch = dispatch.Create("testAction")! with { PublishedAt = DateTime.UtcNow } });
    Check(await OutboxLease.AwaitResult(db, row, row.ClaimId!, TimeSpan.FromMinutes(1), token), "confirmed publish releases delivery lease into awaitingResult");
    db.ChangeTracker.Clear();
    var saved = await db.Outbox.SingleAsync(o => o.Id == id);
    Check(saved.Status == "awaitingResult" && saved.ClaimId == null && saved.LeaseUntil == null &&
        !await OutboxLease.Due(db, DateTime.UtcNow.AddDays(2)).AnyAsync(o => o.Id == id) &&
        (await db.Cases.SingleAsync(c => c.Id == row.CaseId)).State == "waiting" &&
        !await db.History.AnyAsync(h => h.CaseId == row.CaseId), "awaiting result preserves workflow state; no long delivery slot or premature history");
}
id = await Command();
await using (var db = Db())
{
    var row = await db.Outbox.SingleAsync(o => o.Id == id);
    var item = await db.Cases.SingleAsync(c => c.Id == row.CaseId); item.State = "done"; item.Version++; await db.SaveChangesAsync();
    try { await actions.Deliver(db, row, token); throw new Exception("Expected 409"); }
    catch (RuleException ex) { Check(ex.Status == 409 && business.Max == 0, "E: stale WaitingState rejected before target execution"); }
    item.State = "waiting"; item.Version++; await db.SaveChangesAsync();
    try { await actions.Deliver(db, row, token); throw new Exception("Expected 409"); }
    catch (RuleException ex) { Check(ex.Status == 409, "E: re-entered state with newer Version cannot accept old operation"); }
    row.Status = "blocked"; await db.SaveChangesAsync();
}

var settings = new OutboxOptions { BusinessConcurrency = 2, EventConcurrency = 3, LeaseDuration = lease,
    PollInterval = TimeSpan.FromMilliseconds(25), AttemptTimeout = TimeSpan.FromSeconds(12) };
var services = new ServiceCollection().AddDbContext<WorkflowDb>(b => b.UseSqlite("Data Source=" + Path.Combine(folder, "checks.db")))
    .AddSingleton(actions).BuildServiceProvider();
using var worker = new NotificationWorker(services.GetRequiredService<IServiceScopeFactory>(), mail, NullLogger<NotificationWorker>.Instance, settings);
var jobs = new List<long>(); for (var n = 0; n < 8; n++) jobs.Add(await Command());
await worker.StartAsync(token);
await Wait(() => Task.FromResult(business.Active == 2), "business capacity filled");
var notify = await Row(); var fastMail = await Row("mail");
await Wait(async () => { await using var db = Db(); return await db.Notifications.AnyAsync(n => n.OutboxId == notify) &&
    await db.Outbox.AnyAsync(o => o.Id == fastMail && o.Status == "sent"); }, "fast lane drained while business blocked");
Check(business.Active == 2 && mail.Calls > 0, "A: mail and notification drain while slow business targets remain open");
// G: hold long enough to pass several lease periods, then verify ownership renewed, not reclaimed.
await Task.Delay(800);
await using (var db = Db())
{
    var held = await db.Outbox.Where(o => jobs.Contains(o.Id) && o.Status == "processing").ToListAsync();
    Check(held.Count == 2 && held.All(o => o.LeaseUntil > DateTime.UtcNow && o.Version > 2), "G: slow in-flight attempts renew their leases");
}
mail.Hold = true; var mailJobs = new List<long>(); for (var n = 0; n < 8; n++) mailJobs.Add(await Row("mail"));
await Wait(() => Task.FromResult(mail.Gate.Active == 3), "event capacity filled");
await Task.Delay(150);
Check(business.Max == 2 && mail.Gate.Max == 3, "B: each lane respects its own configured concurrency cap");
mail.Gate.Release(); business.Release();
await Wait(async () => { await using var db = Db(); return await db.Outbox.CountAsync(o => jobs.Contains(o.Id) && o.Status == "sent") == 8 &&
    await db.Outbox.CountAsync(o => mailJobs.Contains(o.Id) && o.Status == "sent") == 8; }, "all jobs completed");
await worker.StopAsync(token);
await using (var db = Db()) Check(await db.Cases.CountAsync(c => c.ProcessVersionId == processId && c.State == "done") == 9,
    "runtime: actual configured completions persisted");
// A workflow concurrency failure rolls back the outbox acknowledgement as well.
id = await Command();
await using (var db = Db())
{
    var row = (await OutboxLease.Claim(db, id, TimeSpan.FromSeconds(5), token))!;
    await actions.Deliver(db, row, token);
    await using (var other = Db())
    {
        var item = await other.Cases.SingleAsync(c => c.Id == row.CaseId);
        item.State = "failed"; item.Version++; await other.SaveChangesAsync();
    }
    try { await OutboxLease.Complete(db, row, row.ClaimId!, token); throw new Exception("Expected concurrency failure"); }
    catch (DbUpdateConcurrencyException)
    {
        await using var check = Db();
        Check((await check.Outbox.SingleAsync(o => o.Id == id)).Status == "processing" &&
            !await check.History.AnyAsync(h => h.CaseId == row.CaseId), "E: late result cannot overwrite newer state; acknowledgement rolls back atomically");
        var blocked = await check.Outbox.SingleAsync(o => o.Id == id); blocked.Status = "blocked"; await check.SaveChangesAsync();
    }
}
services.Dispose();

// Cooperative timeout and shutdown/restart recovery exercise the actual BackgroundService lifecycle.
var timeoutGate = new Gate();
var timeoutActions = new BusinessActions([new("testAction", "Test", ["value"], async (_, ct) =>
    { await timeoutGate.Run(ct); return new(true, "completed"); }, (_, _) => Task.FromResult<BusinessResult?>(null))]);
using var recoveryServices = new ServiceCollection().AddDbContext<WorkflowDb>(b => b.UseSqlite("Data Source=" + Path.Combine(folder, "checks.db")))
    .AddSingleton(timeoutActions).BuildServiceProvider();
var shortSettings = new OutboxOptions { BusinessConcurrency = 1, EventConcurrency = 1, LeaseDuration = TimeSpan.FromMilliseconds(150),
    PollInterval = TimeSpan.FromMilliseconds(25), AttemptTimeout = TimeSpan.FromMilliseconds(300) };
id = await Command();
using (var timedWorker = new NotificationWorker(recoveryServices.GetRequiredService<IServiceScopeFactory>(), mail, NullLogger<NotificationWorker>.Instance, shortSettings))
{
    await timedWorker.StartAsync(token);
    await Wait(async () => { await using var db = Db(); return await db.Outbox.AnyAsync(o => o.Id == id && o.Attempts == 1 && o.Status == "pending"); }, "attempt timeout");
    await timedWorker.StopAsync(token);
    await using var db = Db(); var row = await db.Outbox.SingleAsync(o => o.Id == id);
    Check((await db.Cases.SingleAsync(c => c.Id == row.CaseId)).State == "waiting" && timeoutGate.Active == 0,
        "timeout: cancelled attempt backs off and does not falsely complete workflow");
    row.Status = "blocked"; await db.SaveChangesAsync();
}
id = await Command();
var recoverySettings = new OutboxOptions { BusinessConcurrency = 1, EventConcurrency = 1, LeaseDuration = TimeSpan.FromMilliseconds(150),
    PollInterval = TimeSpan.FromMilliseconds(25), AttemptTimeout = TimeSpan.FromSeconds(10) };
using (var stoppedWorker = new NotificationWorker(recoveryServices.GetRequiredService<IServiceScopeFactory>(), mail, NullLogger<NotificationWorker>.Instance, recoverySettings))
{
    await stoppedWorker.StartAsync(token);
    await Wait(() => Task.FromResult(timeoutGate.Active == 1), "worker processing before stop");
    await stoppedWorker.StopAsync(token);
}
await Task.Delay(200);
timeoutGate.Release();
using (var replacement = new NotificationWorker(recoveryServices.GetRequiredService<IServiceScopeFactory>(), mail, NullLogger<NotificationWorker>.Instance, recoverySettings))
{
    await replacement.StartAsync(token);
    await Wait(async () => { await using var db = Db(); return await db.Outbox.AnyAsync(o => o.Id == id && o.Status == "sent"); }, "replacement worker recovery");
    await replacement.StopAsync(token);
    Check(true, "D: replacement BackgroundService recovers and processes stopped worker's expired lease");
}
Console.WriteLine("PASS outbox acceptance checks (isolated temporary SQLite). Fixture: " + folder);

sealed class Gate
{
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int active, maximum;
    public int Active => Volatile.Read(ref active);
    public int Max => Volatile.Read(ref maximum);
    public async Task Run(CancellationToken token)
    {
        var count = Interlocked.Increment(ref active);
        int old; do { old = Max; if (count <= old) break; } while (Interlocked.CompareExchange(ref maximum, count, old) != old);
        try { await release.Task.WaitAsync(token); } finally { Interlocked.Decrement(ref active); }
    }
    public void Release() => release.TrySetResult();
}
sealed class TestMail : IMailSender
{
    public Gate Gate { get; } = new();
    public volatile bool Hold;
    public int Calls;
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref Calls); if (Hold) await Gate.Run(cancellationToken); }
}
