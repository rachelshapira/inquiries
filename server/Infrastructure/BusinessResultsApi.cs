using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Workflow.Infrastructure;

public sealed class BusinessCallbackOptions
{
    public bool Enabled { get; init; }
    public Dictionary<string, string> LocalKeys { get; init; } = [];
    public string Authority { get; init; } = "";
    public string Audience { get; init; } = "";
}

// Local-only machine authentication. Production uses its own organizational JWT scheme.
public sealed class LocalBusinessAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, BusinessCallbackOptions callbacks)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))
            return Task.FromResult(AuthenticateResult.Fail("Local integration only"));
        var supplied = Request.Headers["X-Integration-Key"];
        if (supplied.Count != 1 || supplied[0] is not { Length: >= 32 and <= 512 } key)
            return Task.FromResult(AuthenticateResult.NoResult());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        foreach (var entry in callbacks.LocalKeys)
            if (CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(Encoding.UTF8.GetBytes(entry.Value))))
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", entry.Key)], Scheme.Name)), Scheme.Name)));
        return Task.FromResult(AuthenticateResult.Fail("Invalid integration identity"));
    }
}

public record CallbackResult(bool? Success, string? Message, Dictionary<string, string>? Data = null);
public record BusinessCallback(string? OperationId, string? CallbackToken, CallbackResult? Result);

public static class BusinessResultsApi
{
    public const string Scheme = "business-callback";
    public static void Register(IServiceCollection services, BusinessCallbackOptions options, bool demo)
    {
        if (!options.Enabled) return;
        services.AddSingleton(options);
        if (demo)
        {
            if (options.LocalKeys.Count == 0 || options.LocalKeys.Any(k => string.IsNullOrWhiteSpace(k.Key) || k.Value.Length < 32 || k.Value.Length > 512) ||
                options.LocalKeys.Values.Distinct().Count() != options.LocalKeys.Count)
                throw new InvalidOperationException("Distinct strong local integration credentials are required");
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, LocalBusinessAuthentication>(Scheme, _ => { });
        }
        else
        {
            if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority) || authority.Scheme != "https" || string.IsNullOrWhiteSpace(options.Audience))
                throw new InvalidOperationException("Business callback JWT authority and audience are required");
            services.AddAuthentication().AddJwtBearer(Scheme, jwt =>
            {
                jwt.Authority = options.Authority; jwt.Audience = options.Audience;
                jwt.MapInboundClaims = false; jwt.RequireHttpsMetadata = true;
            });
        }
    }

    public static void Map(WebApplication app, bool demo = false)
    {
        app.MapPost("/integrations/business-actions/result", async (HttpContext context, WorkflowDb db) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps && !(demo && context.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)))
                return Results.StatusCode(403);
            // Set before reading, including requests with chunked bodies.
            var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 64 * 1024;
            if (context.Request.ContentLength > 64 * 1024)
            {
                // The unread oversized HTTP/1 body must not poison a reused connection.
                if (context.Request.Protocol.StartsWith("HTTP/1", StringComparison.Ordinal)) context.Response.Headers.Connection = "close";
                return Results.StatusCode(413);
            }
            if (!context.Request.HasJsonContentType()) return Results.StatusCode(415);
            BusinessCallback? input;
            try { input = await context.Request.ReadFromJsonAsync<BusinessCallback>(context.RequestAborted); }
            catch (BadHttpRequestException ex) when (ex.StatusCode == 413)
            {
                if (context.Request.Protocol.StartsWith("HTTP/1", StringComparison.Ordinal)) context.Response.Headers.Connection = "close";
                return Results.StatusCode(413);
            }
            var duplicate = await Complete(db, input, context.User.FindFirstValue("sub") ?? "", context.RequestAborted);
            return Results.Ok(new { accepted = true, duplicate });
        }).RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = Scheme });
    }

    public static async Task<bool> Complete(WorkflowDb db, BusinessCallback? input, string targetId, CancellationToken cancellation, string? recoveryClaim = null)
    {
        Engine.Require(input?.Result?.Success != null && !string.IsNullOrWhiteSpace(input.Result.Message) && input.Result.Message.Length <= 4000 &&
            (input.Result.Data == null || (input.Result.Data.Count <= 32 && input.Result.Data.All(p => !string.IsNullOrWhiteSpace(p.Key) && p.Key.Length <= 100 && p.Value != null && p.Value.Length <= 4000))),
            "תוצאת הפעולה אינה תקינה");
        Engine.Require(long.TryParse(input!.OperationId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 &&
            id.ToString(CultureInfo.InvariantCulture) == input.OperationId, "מזהה הפעולה אינו תקין");
        var result = new BusinessResult(input.Result!.Success!.Value, input.Result.Message!, input.Result.Data);
        var message = await db.Outbox.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id && o.Kind == "businessAction", cancellation);
        Engine.Require(message != null, "הדיווח אינו מורשה", 403);
        var job = Json.Read<BusinessJob>(message!.Message);
        Engine.Require(job.Dispatch != null && !string.IsNullOrEmpty(targetId) && job.Dispatch.TargetId == targetId &&
            BusinessDispatch.ValidToken(job.Dispatch, input.CallbackToken ?? ""), "הדיווח אינו מורשה", 403);
        if (message.Status == "sent")
        {
            Engine.Require(Same(job.Result, result), "תוצאה סותרת לפעולה שהושלמה", 409);
            return true;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        // Win ownership of completion before loading the case. Publisher/poller/callback share this row fence.
        var changed = await db.Outbox.Where(o => o.Id == id && o.Version == message.Version &&
            (recoveryClaim == null || (o.ClaimId == recoveryClaim && o.LeaseUntil > DateTime.UtcNow)) &&
            (o.Status == "pending" || o.Status == "processing" || o.Status == "awaitingResult" || o.Status == "blocked"))
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Status, "sent").SetProperty(o => o.ClaimId, (string?)null)
                .SetProperty(o => o.LeaseUntil, (DateTime?)null).SetProperty(o => o.Version, o => o.Version + 1), cancellation);
        if (changed != 1)
        {
            await transaction.RollbackAsync(cancellation);
            var current = await db.Outbox.AsNoTracking().SingleAsync(o => o.Id == id, cancellation);
            Engine.Require(current.Status == "sent" && Same(Json.Read<BusinessJob>(current.Message).Result, result), "הפעולה עודכנה במקביל; יש לנסות שוב", 409);
            return true;
        }
        // Engine checks WaitingState + WaitingVersion and applies configured success/failure transition.
        await BusinessActions.Complete(db, message, result);
        await db.Outbox.Where(o => o.Id == id).ExecuteUpdateAsync(set => set.SetProperty(o => o.Message, message.Message), cancellation);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return false;
    }

    private static bool Same(BusinessResult? a, BusinessResult b) => a != null && a.Success == b.Success && a.Message == b.Message &&
        (a.Data?.Count ?? 0) == (b.Data?.Count ?? 0) && (a.Data == null || a.Data.All(p => b.Data != null && b.Data.TryGetValue(p.Key, out var value) && value == p.Value));
}
