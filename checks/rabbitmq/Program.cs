using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workflow;
using Workflow.Infrastructure;

// Real local broker; isolated queue names. No inquiry data or main application configuration.
var suffix = Guid.NewGuid().ToString("N");
var options = new RabbitMqOptions
{
    User = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "",
    Exchange = "checks.business." + suffix, Queue = "checks.commands." + suffix
};
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var cancellation = timeout.Token;
var factory = new ConnectionFactory { HostName = options.Host, UserName = options.User,
    Password = options.Password, VirtualHost = options.VirtualHost, AutomaticRecoveryEnabled = false };
await using var connection = await factory.CreateConnectionAsync(cancellation);
await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellation);
await using var publisher = new RabbitMqPublisher(options);
var command = new BusinessCommand(1, "testAction", new("operation-" + suffix, 1, new() { ["text"] = "נתונים דמיוניים" }),
    "test-token-only", "http://127.0.0.1:5081/integrations/business-actions/result");
try
{
    await publisher.Publish(command, cancellation);
    var received = await channel.BasicGetAsync(options.Queue, autoAck: false, cancellation);
    if (received == null || received.BasicProperties.MessageId != command.Request.OperationId ||
        !received.BasicProperties.Persistent || Json.Read<BusinessCommand>(Encoding.UTF8.GetString(received.Body.Span)).Request.Inputs["text"] != "נתונים דמיוניים")
        throw new Exception("Persistent command / identity / Hebrew payload failed");
    await channel.BasicNackAsync(received.DeliveryTag, multiple: false, requeue: true, cancellation);
    Console.WriteLine("PASS real RabbitMQ: confirmed persistent command; stable OperationId and Hebrew payload; consumer can requeue");

    // A new connection sees the requeued message. This does NOT assert broker restart durability.
    await using (var secondConnection = await factory.CreateConnectionAsync(cancellation))
    await using (var second = await secondConnection.CreateChannelAsync(cancellationToken: cancellation))
    {
        var again = await second.BasicGetAsync(options.Queue, autoAck: true, cancellation);
        if (again == null || !again.Redelivered) throw new Exception("Requeued delivery unavailable to replacement consumer");
    }
    Console.WriteLine("PASS real RabbitMQ: replacement consumer receives uncompleted command");

    await channel.QueueUnbindAsync(options.Queue, options.Exchange, options.RoutingKey, cancellationToken: cancellation);
    var rejected = false;
    try { await publisher.Publish(command, cancellation); }
    catch (PublishException) { rejected = true; }
    if (!rejected) throw new Exception("Unroutable command falsely acknowledged");
    Console.WriteLine("PASS real RabbitMQ: mandatory unroutable publish fails instead of false success");
    await channel.QueueBindAsync(options.Queue, options.Exchange, options.RoutingKey, cancellationToken: cancellation);
    await publisher.Publish(command, cancellation);
    if (await channel.BasicGetAsync(options.Queue, autoAck: true, cancellation) == null) throw new Exception("Retry after routing recovery failed");
    Console.WriteLine("PASS real RabbitMQ: publish succeeds after routing restored; no business completion claimed");

    var folder = Path.Combine(Path.GetTempPath(), "workflow-rabbitmq-" + suffix);
    Directory.CreateDirectory(folder);
    var connectionString = "Data Source=" + Path.Combine(folder, "checks.db");
    var dbOptions = new DbContextOptionsBuilder<WorkflowDb>().UseSqlite(connectionString).Options;
    var protector = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(folder, "keys")), builder =>
    {
        builder.SetApplicationName("Workflow.BusinessCallbacks");
        if (OperatingSystem.IsWindows()) builder.ProtectKeysWithDpapi();
    }).CreateProtector("BusinessCallbacks.v1");
    var dispatch = new BusinessDispatch(new BusinessQueueOptions { ActionKeys = ["testAction"], TargetIds = new() { ["testAction"] = "test-target" }, CallbackUrl = command.CallbackUrl }, protector);
    var spec = new BusinessActionSpec("testAction", new(), "complete", "fail");
    var definition = new Definition("Queue check", "review", [],
        [new("review", "Review", false), new("waiting", "Waiting", false), new("done", "Done", true), new("failed", "Failed", true)],
        [new("approve", "Approve", "review", "waiting", ["Reviewer"], "none", [], BusinessAction: spec),
         new("complete", "Complete", "waiting", "done", ["Admin"], "none", [], Trigger: "businessSuccess"),
         new("fail", "Fail", "waiting", "failed", ["Admin"], "none", [], Trigger: "businessFailure")], []);
    var ids = new List<long>();
    await using (var db = new WorkflowDb(dbOptions))
    {
        await db.Database.EnsureCreatedAsync(cancellation); await Seed.Initialize(db);
        var process = new ProcessVersion { Key = "queue-check", DefinitionJson = Json.Write(definition) };
        db.Processes.Add(process); await db.SaveChangesAsync(cancellation);
        for (var i = 0; i < 6; i++)
        {
            var item = new Case { ProviderId = 1, ProcessVersionId = process.Id, State = "waiting", Title = "נתונים דמיוניים", Unit = "care" };
            db.Cases.Add(item); await db.SaveChangesAsync(cancellation);
            var job = new BusinessJob(spec, "reviewer", "approve", "review", "waiting", 1, new(), WaitingVersion: item.Version, Dispatch: dispatch.Create("testAction"));
            var row = new Outbox { CaseId = item.Id, Kind = "businessAction", Message = Json.Write(job) };
            db.Outbox.Add(row); await db.SaveChangesAsync(cancellation); ids.Add(row.Id);
        }
    }
    var handler = new BusinessHandler("testAction", "Test", [], (_, _) => throw new Exception("Publisher must not run target"),
        (_, _) => Task.FromResult<BusinessResult?>(null));
    await using var services = new ServiceCollection().AddDbContext<WorkflowDb>(b => b.UseSqlite(connectionString))
        .AddSingleton(dispatch).AddSingleton(new BusinessActions([handler], dispatch)).AddSingleton<IBusinessPublisher>(publisher).BuildServiceProvider();
    using var worker = new NotificationWorker(services.GetRequiredService<IServiceScopeFactory>(), new NullMailSender(),
        NullLogger<NotificationWorker>.Instance, new OutboxOptions { BusinessConcurrency = 1, EventConcurrency = 1, PollInterval = TimeSpan.FromMilliseconds(25) });
    await worker.StartAsync(cancellation);
    try
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            await using var db = new WorkflowDb(dbOptions);
            if (await db.Outbox.CountAsync(o => ids.Contains(o.Id) && o.Status == "awaitingResult", cancellation) == 6)
            {
                if (await db.Outbox.AnyAsync(o => ids.Contains(o.Id) && (o.ClaimId != null || o.LeaseUntil != null), cancellation)) throw new Exception("Dispatch retained lease");
                break;
            }
            await Task.Delay(25, cancellation);
        }
    }
    finally { await worker.StopAsync(CancellationToken.None); }
    for (var i = 0; i < 6; i++)
    {
        var delivered = await channel.BasicGetAsync(options.Queue, autoAck: true, cancellation) ?? throw new Exception("Worker command absent from real queue");
        var body = Json.Read<BusinessCommand>(Encoding.UTF8.GetString(delivered.Body.Span));
        await using var db = new WorkflowDb(dbOptions);
        var row = await db.Outbox.SingleAsync(o => o.Id == long.Parse(body.Request.OperationId), cancellation);
        if (!BusinessDispatch.ValidToken(Json.Read<BusinessJob>(row.Message).Dispatch!, body.CallbackToken) ||
            (await db.Cases.SingleAsync(c => c.Id == row.CaseId, cancellation)).State != "waiting") throw new Exception("Worker identity/state mismatch");
    }
    Console.WriteLine("PASS real dispatcher + RabbitMQ: one publishing slot dispatches six waiting operations, releases all leases, never runs target or completes workflow");
}
finally
{
    await channel.QueueDeleteAsync(options.Queue, ifUnused: false, ifEmpty: false, cancellationToken: CancellationToken.None);
    await channel.ExchangeDeleteAsync(options.Exchange, ifUnused: false, cancellationToken: CancellationToken.None);
}
