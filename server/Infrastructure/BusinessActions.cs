using Microsoft.EntityFrameworkCore;

namespace Workflow.Infrastructure;

public record BusinessResult(bool Success, string Message, Dictionary<string, string>? Data = null);
public record BusinessRequest(string OperationId, int ProviderId, Dictionary<string, string> Inputs);
public record BusinessHandler(string Key, string Label, string[] Inputs, Func<BusinessRequest, CancellationToken, Task<BusinessResult>> Run, Func<BusinessRequest, CancellationToken, Task<BusinessResult?>> Recover);
// Optional asynchronous target receipt lookup; legacy handlers keep their original target.
public record AsyncBusinessTarget(string Key, Func<BusinessRequest, CancellationToken, Task<BusinessResult?>> Recover);
public record BusinessJob(BusinessActionSpec Spec, string ActorId, string OriginAction, string OriginState, string WaitingState,
    int ProviderId, Dictionary<string, string> Inputs, BusinessResult? Result = null, long? WaitingVersion = null, AsyncDelivery? Dispatch = null);

// Workflow knows configured transitions; application handlers own business operations.
public sealed class BusinessActions(IEnumerable<BusinessHandler> handlers, BusinessDispatch? dispatch = null, IEnumerable<AsyncBusinessTarget>? asyncTargets = null)
{
    private readonly Dictionary<string, BusinessHandler> catalog = handlers.ToDictionary(h => h.Key);
    private readonly Dictionary<string, AsyncBusinessTarget> recoveryTargets = (asyncTargets ?? []).ToDictionary(h => h.Key);
    public object Catalog => catalog.Values.Select(h => new { h.Key, h.Label, h.Inputs });

    public void Validate(Definition definition)
    {
        foreach (var transition in definition.Transitions.Where(t => t.BusinessAction != null))
        {
            var spec = transition.BusinessAction!;
            Engine.Require(!string.IsNullOrWhiteSpace(spec.Key), "יש לבחור פעולה עסקית");
            Engine.Require(catalog.TryGetValue(spec.Key, out var handler), "הפעולה העסקית אינה זמינה במערכת זו");
            Engine.Require(spec.Inputs != null && spec.Inputs.Keys.ToHashSet().SetEquals(handler!.Inputs), "יש לקשר את כל קלטי הפעולה לשדות הטופס");
            Engine.Require(spec.Inputs!.Values.All(key => definition.Fields.Any(f => f.Key == key && f.Required && f.Type is "text" or "textarea")), "קלט הפעולה חייב להפנות לשדה טקסט חובה");
        }
    }

    public void Enqueue(WorkflowDb db, Case item, Account actor, Transition transition)
    {
        if (transition.BusinessAction is not { } spec) return;
        Validate(Engine.Definition(item));
        var data = Json.Read<Dictionary<string, string>>(item.DataJson);
        Engine.Require(spec.Inputs.Values.All(k => data.TryGetValue(k, out var value) && !string.IsNullOrWhiteSpace(value)), "חסר קלט לפעולה העסקית");
        var job = new BusinessJob(spec, actor.Id, transition.Key, transition.From, item.State, item.ProviderId,
            spec.Inputs.ToDictionary(pair => pair.Key, pair => data[pair.Value]), WaitingVersion: item.Version, Dispatch: dispatch?.Create(spec.Key));
        db.Outbox.Add(new Outbox { CaseId = item.Id, Kind = "businessAction", Message = Json.Write(job) });
    }

    public async Task Deliver(WorkflowDb db, Outbox message, CancellationToken cancellation)
    {
        var job = Json.Read<BusinessJob>(message.Message);
        Engine.Require(job.Dispatch == null, "פעולה אסינכרונית נשלחת לתור ואינה מבוצעת במסלול הסינכרוני", 409);
        var item = await LoadWaiting(db, message, job);
        BusinessResult result;
        var allowed = await AuthorizerStillAllowed(db, item, job, cancellation);
        var request = new BusinessRequest(message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), job.ProviderId, job.Inputs);
        if (!catalog.TryGetValue(job.Spec.Key, out var handler))
            throw new InvalidOperationException("Business handler unavailable; outcome requires reconciliation");
        else if (await handler.Recover(request, cancellation) is { } recovered)
            result = recovered; // A committed target receipt wins even if permissions changed after delivery.
        else if (!allowed)
            result = new(false, "הפעולה לא בוצעה: הרשאת המאשר אינה תקפה עוד.");
        else
            result = await handler.Run(request, cancellation);

        ApplyResult(db, message, item, job, result);
    }

    private static async Task<bool> AuthorizerStillAllowed(WorkflowDb db, Case item, BusinessJob job, CancellationToken cancellation)
    {
        var actor = await db.Accounts.SingleOrDefaultAsync(a => a.Id == job.ActorId && a.Active, cancellation);
        if (actor != null)
        {
            actor.Scope = await OrgTree.Scope(db, actor.Unit, actor.ReadAccess);
            actor.WriteScope = await OrgTree.Scope(db, actor.Unit, actor.WriteAccess);
        }
        var origin = Engine.Definition(item).Transitions.Single(t => t.Key == job.OriginAction && t.From == job.OriginState);
        return actor != null && Engine.CanWrite(actor, item) && origin.Roles.Contains(actor.Role);
    }

    // true = broker accepted; false = a local result was staged and needs normal atomic completion.
    public async Task<bool> Publish(WorkflowDb db, Outbox message, IBusinessPublisher publisher, CancellationToken cancellation)
    {
        var job = Json.Read<BusinessJob>(message.Message);
        if (dispatch == null || job.Dispatch == null) throw new InvalidOperationException("Asynchronous dispatch unavailable");
        var item = await LoadWaiting(db, message, job);
        if (!catalog.TryGetValue(job.Spec.Key, out var handler))
            throw new InvalidOperationException("Business handler unavailable; outcome requires reconciliation");
        var command = dispatch.Command(message);
        if (!await AuthorizerStillAllowed(db, item, job, cancellation))
        {
            var recovered = await RecoverTarget(job, command.Request, cancellation);
            ApplyResult(db, message, item, job, recovered ?? new(false, "הפעולה לא בוצעה: הרשאת המאשר אינה תקפה עוד."));
            return false;
        }
        await publisher.Publish(command, cancellation);
        message.Message = Json.Write(job with { Dispatch = job.Dispatch with { PublishedAt = DateTime.UtcNow } });
        return true;
    }

    public async Task<BusinessResult?> Recover(WorkflowDb db, Outbox message, CancellationToken cancellation)
    {
        var job = Json.Read<BusinessJob>(message.Message);
        await LoadWaiting(db, message, job);
        return await RecoverTarget(job, new(message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), job.ProviderId, job.Inputs), cancellation);
    }

    private Task<BusinessResult?> RecoverTarget(BusinessJob job, BusinessRequest request, CancellationToken cancellation) =>
        recoveryTargets.TryGetValue(job.Spec.Key, out var target) ? target.Recover(request, cancellation) :
        throw new InvalidOperationException("Asynchronous target recovery is not configured");

    // Trusted infrastructure entry point. Callback authentication/fencing will wrap this later.
    // Caller must commit result, workflow changes and acknowledgement atomically.
    public static async Task Complete(WorkflowDb db, Outbox message, BusinessResult result)
    {
        Engine.Require(message.Kind == "businessAction", "ההודעה אינה פעולה עסקית", 409);
        var job = Json.Read<BusinessJob>(message.Message);
        var item = await LoadWaiting(db, message, job);
        ApplyResult(db, message, item, job, result);
    }

    private static async Task<Case> LoadWaiting(WorkflowDb db, Outbox message, BusinessJob job)
    {
        var item = await Engine.Load(db, message.CaseId, SystemAccount());
        // State/version identify the waiting step; the caller separately fences delivery ownership.
        Engine.Require(item.State == job.WaitingState && item.ProviderId == job.ProviderId &&
            (job.WaitingVersion == null || item.Version == job.WaitingVersion), "הפנייה אינה ממתינה עוד לפעולה העסקית", 409);
        return item;
    }

    private static Account SystemAccount() => new() { Name = "מערכת", Role = "Admin", Scope = new(true, []), WriteScope = new(true, []) };

    private static void ApplyResult(WorkflowDb db, Outbox message, Case item, BusinessJob job, BusinessResult result)
    {
        var completion = Engine.Execute(item, SystemAccount(), result.Success ? job.Spec.Success : job.Spec.Failure,
            result.Message, true, result.Success ? "businessSuccess" : "businessFailure");
        message.Message = Json.Write(job with { Result = result });
        var mail = completion.Effects.Contains("sendMail");
        db.Outbox.Add(new Outbox { CaseId = item.Id, Kind = mail ? "mail" : "notification",
            Recipient = mail ? item.Provider.Email : "", Message = item.Title + ": " + result.Message });
        // Caller saves result, completion, history, notification and acknowledgement atomically.
    }
}
