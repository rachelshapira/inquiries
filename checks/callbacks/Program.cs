using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Workflow;
using Workflow.Infrastructure;

var folder = Path.Combine(Path.GetTempPath(), "workflow-callback-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var connection = "Data Source=" + Path.Combine(folder, "checks.db");
var options = new DbContextOptionsBuilder<WorkflowDb>().UseSqlite(connection).Options;
WorkflowDb Db() => new(options);
var machineKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddDbContext<WorkflowDb>(b => b.UseSqlite(connection));
builder.Services.AddAuthentication("human").AddCookie("human", o => o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; });
builder.Services.AddAuthorization();
BusinessResultsApi.Register(builder.Services, new BusinessCallbackOptions { Enabled = true, LocalKeys = new() { ["test-target"] = machineKey, ["other-target"] = otherKey } }, demo: true);
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (RuleException ex) { context.Response.StatusCode = ex.Status; }
    catch (DbUpdateConcurrencyException) { context.Response.StatusCode = 409; }
    catch (System.Text.Json.JsonException) { context.Response.StatusCode = 400; }
    catch (BadHttpRequestException ex) { context.Response.StatusCode = ex.StatusCode; }
    catch (Exception ex) { Console.WriteLine("Callback test host failure: " + ex); context.Response.StatusCode = 500; }
});
app.UseAuthentication(); app.UseAuthorization();
BusinessResultsApi.Map(app, demo: true);
app.MapPost("/test/human", async (HttpContext ctx) => { await ctx.SignInAsync("human", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "admin")], "human"))); return Results.Ok(); });
app.MapGet("/test/human-only", () => Results.Ok()).RequireAuthorization();
var keys = new DirectoryInfo(Path.Combine(folder, "keys"));
var protector = DataProtectionProvider.Create(keys, b => { b.SetApplicationName("Workflow.BusinessCallbacks"); if (OperatingSystem.IsWindows()) b.ProtectKeysWithDpapi(); }).CreateProtector("BusinessCallbacks.v1");
var dispatch = new BusinessDispatch(new BusinessQueueOptions { ActionKeys = ["testAction"], TargetIds = new() { ["testAction"] = "test-target" }, CallbackUrl = "http://127.0.0.1/integrations/business-actions/result" }, protector);
var spec = new BusinessActionSpec("testAction", new(), "complete", "fail");
var definition = new Definition("Callback check", "review", [],
    [new("review", "Review", false), new("waiting", "Waiting", false), new("done", "Done", true), new("failed", "Failed", true)],
    [new("approve", "Approve", "review", "waiting", ["Reviewer"], "none", [], BusinessAction: spec),
     new("complete", "Complete", "waiting", "done", ["Admin"], "none", [], Trigger: "businessSuccess"),
     new("fail", "Fail", "waiting", "failed", ["Admin"], "none", [], Trigger: "businessFailure")], []);
int processId;
await using (var db = Db())
{
    await db.Database.EnsureCreatedAsync(); await Seed.Initialize(db);
    var process = new ProcessVersion { Key = "callback-check", DefinitionJson = Json.Write(definition) };
    db.Processes.Add(process); await db.SaveChangesAsync(); processId = process.Id;
}
async Task<(Outbox Row, BusinessCommand Command)> Operation()
{
    await using var db = Db();
    var item = new Case { ProviderId = 1, ProcessVersionId = processId, State = "waiting", Title = "בדיקת תוצאה דמיונית", Unit = "care" };
    db.Cases.Add(item); await db.SaveChangesAsync();
    var job = new BusinessJob(spec, "reviewer", "approve", "review", "waiting", 1, new(), WaitingVersion: item.Version, Dispatch: dispatch.Create("testAction"));
    var row = new Outbox { CaseId = item.Id, Kind = "businessAction", Status = "awaitingResult", Message = Json.Write(job) };
    db.Outbox.Add(row); await db.SaveChangesAsync(); return (row, dispatch.Command(row));
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
await app.StartAsync();
try
{
    var url = app.Urls.Single();
    using var client = new HttpClient { BaseAddress = new Uri(url) };
    async Task<HttpResponseMessage> Send(BusinessCallback input, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/business-actions/result") { Content = JsonContent.Create(input) };
        if (key != null) request.Headers.Add("X-Integration-Key", key);
        return await client.SendAsync(request);
    }
    var op = await Operation();
    var payload = new BusinessCallback(op.Command.Request.OperationId, op.Command.CallbackToken, new(true, "הפעולה הושלמה", new() { ["before"] = "ישן", ["after"] = "חדש" }));
    Check((await Send(payload)).StatusCode == HttpStatusCode.Unauthorized && (await Send(payload, new string('x', 44))).StatusCode == HttpStatusCode.Unauthorized, "missing/wrong machine credentials rejected");
    await client.PostAsync("/test/human", null);
    Check((await Send(payload)).StatusCode == HttpStatusCode.Unauthorized, "valid human Admin cookie cannot authenticate callback");
    using (var machine = new HttpClient { BaseAddress = client.BaseAddress })
    {
        machine.DefaultRequestHeaders.Add("X-Integration-Key", machineKey);
        Check((await machine.GetAsync("/test/human-only")).StatusCode == HttpStatusCode.Unauthorized, "machine credentials do not authorize human APIs");
    }
    Check((await Send(payload, otherKey)).StatusCode == HttpStatusCode.Forbidden &&
        (await Send(payload with { CallbackToken = "wrong" }, machineKey)).StatusCode == HttpStatusCode.Forbidden, "wrong target identity / operation token rejected");
    var unrelated = await Operation();
    Check((await Send(payload with { OperationId = unrelated.Command.Request.OperationId }, machineKey)).StatusCode == HttpStatusCode.Forbidden,
        "valid token from another operation cannot be replayed");
    Check((await Send(payload with { Result = new(null, "missing success") }, machineKey)).StatusCode == HttpStatusCode.BadRequest, "incomplete result rejected before state change");
    using (var invalid = new HttpRequestMessage(HttpMethod.Post, "/integrations/business-actions/result") { Content = new StringContent("not json") })
    {
        invalid.Headers.Add("X-Integration-Key", machineKey);
        Check((await client.SendAsync(invalid)).StatusCode == HttpStatusCode.UnsupportedMediaType, "non-JSON content rejected without server error");
    }
    using (var huge = new HttpRequestMessage(HttpMethod.Post, "/integrations/business-actions/result") { Content = new StringContent(new string('x', 70000)) })
    {
        huge.Headers.Add("X-Integration-Key", machineKey);
        Check((await client.SendAsync(huge)).StatusCode == HttpStatusCode.RequestEntityTooLarge, "oversized callback rejected");
    }
    Check((await Send(payload, machineKey)).StatusCode == HttpStatusCode.OK, "authenticated callback executes configured success");
    Check((await Send(payload with { Result = new(true, "הפעולה הושלמה", new() { ["after"] = "חדש", ["before"] = "ישן" }) }, machineKey)).StatusCode == HttpStatusCode.OK,
        "same result with reordered data acknowledged without duplication");
    Check((await Send(payload with { Result = new(false, "conflicting result") }, machineKey)).StatusCode == HttpStatusCode.Conflict, "conflicting duplicate rejected");
    await using (var db = Db())
    {
        var stored = await db.Outbox.SingleAsync(o => o.Id == op.Row.Id);
        Check(stored.Status == "sent" && stored.ClaimId == null && Json.Read<BusinessJob>(stored.Message).Result!.Success &&
            (await db.Cases.SingleAsync(c => c.Id == op.Row.CaseId)).State == "done" &&
            await db.History.CountAsync(h => h.CaseId == op.Row.CaseId) == 1 &&
            await db.Outbox.CountAsync(o => o.CaseId == op.Row.CaseId && o.Kind != "businessAction") == 1,
            "result / configured state / single history / continuation outbox committed together");
    }
    foreach (var success in new[] { false, true })
    {
        op = await Operation(); payload = new(op.Command.Request.OperationId, op.Command.CallbackToken, new(success, "reported result"));
        var responses = await Task.WhenAll(Send(payload, machineKey), Send(payload, machineKey));
        Check(responses.All(r => r.StatusCode == HttpStatusCode.OK), "competing identical callbacks converge: " + success);
        await using var db = Db();
        Check((await db.Cases.SingleAsync(c => c.Id == op.Row.CaseId)).State == (success ? "done" : "failed") &&
            await db.History.CountAsync(h => h.CaseId == op.Row.CaseId) == 1, "one configured completion despite competing callbacks");
    }
    op = await Operation(); payload = new(op.Command.Request.OperationId, op.Command.CallbackToken, new(true, "success result"));
    var conflicting = await Task.WhenAll(Send(payload, machineKey), Send(payload with { Result = new(false, "failure result") }, machineKey));
    Check(conflicting.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && conflicting.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
        "competing conflicting results: one accepted, one rejected");
    await using (var db = Db()) Check(await db.History.CountAsync(h => h.CaseId == op.Row.CaseId) == 1, "conflicting race still records one completion");
    op = await Operation(); payload = new(op.Command.Request.OperationId, op.Command.CallbackToken, new(true, "late result"));
    await using (var db = Db()) { var item = await db.Cases.SingleAsync(c => c.Id == op.Row.CaseId); item.Version++; await db.SaveChangesAsync(); }
    Check((await Send(payload, machineKey)).StatusCode == HttpStatusCode.Conflict, "late result for newer WaitingVersion rejected");
    await using (var db = Db()) Check((await db.Outbox.SingleAsync(o => o.Id == op.Row.Id)).Status == "awaitingResult" && !await db.History.AnyAsync(h => h.CaseId == op.Row.CaseId), "stale result rolls back completion ownership and all effects");
    op = await Operation(); payload = new(op.Command.Request.OperationId, op.Command.CallbackToken, new(true, "early result"));
    Outbox claimed;
    await using (var db = Db()) { var row = await db.Outbox.SingleAsync(o => o.Id == op.Row.Id); row.Status = "pending"; await db.SaveChangesAsync(); claimed = (await OutboxLease.Claim(db, row.Id, TimeSpan.FromSeconds(30), CancellationToken.None))!; }
    Check((await Send(payload, machineKey)).StatusCode == HttpStatusCode.OK, "callback before publisher acknowledgement completes normally");
    await using (var db = Db()) Check(!await OutboxLease.AwaitResult(db, claimed, claimed.ClaimId!, TimeSpan.FromMinutes(1), CancellationToken.None) &&
        (await db.Outbox.AsNoTracking().SingleAsync(o => o.Id == claimed.Id)).Status == "sent", "late publisher cannot overwrite early callback");
    op = await Operation(); payload = new(op.Command.Request.OperationId, op.Command.CallbackToken, new(true, "result after reconciliation deadline"));
    await using (var db = Db())
    {
        var row = await db.Outbox.SingleAsync(o => o.Id == op.Row.Id);
        var job = Json.Read<BusinessJob>(row.Message);
        row.Message = Json.Write(job with { Dispatch = job.Dispatch! with { DeadlineAt = DateTime.UtcNow.AddHours(-1) } });
        await db.SaveChangesAsync();
    }
    Check((await Send(payload, machineKey)).StatusCode == HttpStatusCode.OK, "logical timeout does not invalidate a still-waiting operation token");
    // Exercise the production transport gate locally; this is not a test of a real JWT issuer.
    var secureBuilder = WebApplication.CreateBuilder();
    secureBuilder.Logging.ClearProviders(); secureBuilder.WebHost.UseUrls("http://127.0.0.1:0");
    secureBuilder.Services.AddDbContext<WorkflowDb>(b => b.UseSqlite(connection));
    secureBuilder.Services.AddAuthorization();
    BusinessResultsApi.Register(secureBuilder.Services, new BusinessCallbackOptions { Enabled = true, LocalKeys = new() { ["test-target"] = machineKey } }, demo: true);
    await using (var secure = secureBuilder.Build())
    {
        secure.UseAuthentication(); secure.UseAuthorization(); BusinessResultsApi.Map(secure);
        await secure.StartAsync();
        using var probe = new HttpClient { BaseAddress = new Uri(secure.Urls.Single()) };
        probe.DefaultRequestHeaders.Add("X-Integration-Key", machineKey);
        Check((await probe.PostAsJsonAsync("/integrations/business-actions/result", payload)).StatusCode == HttpStatusCode.Forbidden,
            "non-demo callback transport requires HTTPS, even on loopback");
        await secure.StopAsync();
    }
    Console.WriteLine("PASS real HTTP callback acceptance checks; isolated SQLite; no target or workflow engine replacement");
}
finally { await app.StopAsync(); await app.DisposeAsync(); }
