using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Workflow.Infrastructure;

public sealed class BusinessQueueOptions
{
    public string[] ActionKeys { get; init; } = [];
    public string CallbackUrl { get; init; } = "";
    public Dictionary<string, string> TargetIds { get; init; } = [];
    public TimeSpan LogicalTimeout { get; init; } = TimeSpan.FromDays(1);
    public TimeSpan RecoveryInterval { get; init; } = TimeSpan.FromMinutes(1);
    public int RecoveryConcurrency { get; init; } = 2;
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public string KeyPath { get; init; } = "";
}

// Delivery metadata, not a second copy of the inquiry's workflow state.
public record AsyncDelivery(string ProtectedToken, string TokenHash, string CallbackUrl,
    DateTime CreatedAt, DateTime DeadlineAt, DateTime? PublishedAt = null, string TargetId = "",
    DateTime? ReconciliationAt = null, int RecoveryFailures = 0);
public record BusinessCommand(int Version, string ActionKey, BusinessRequest Request, string CallbackToken, string CallbackUrl);

public sealed class BusinessDispatch(BusinessQueueOptions options, IDataProtector protector)
{
    public TimeSpan RecoveryInterval => options.RecoveryInterval;

    public AsyncDelivery? Create(string actionKey)
    {
        if (!options.ActionKeys.Contains(actionKey, StringComparer.Ordinal)) return null;
        if (!options.TargetIds.TryGetValue(actionKey, out var targetId) || string.IsNullOrWhiteSpace(targetId))
            throw new InvalidOperationException("Business action target identity is required");
        if (!Uri.TryCreate(options.CallbackUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            options.LogicalTimeout <= TimeSpan.Zero || options.RecoveryInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("Invalid business callback configuration");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        return new(protector.Protect(token), Hash(token), options.CallbackUrl, now, now.Add(options.LogicalTimeout), TargetId: targetId);
    }

    public BusinessCommand Command(Outbox message)
    {
        var job = Json.Read<BusinessJob>(message.Message);
        var delivery = job.Dispatch ?? throw new InvalidOperationException("Not an asynchronous business operation");
        var token = protector.Unprotect(delivery.ProtectedToken);
        if (!ValidToken(delivery, token)) throw new CryptographicException("Callback token integrity check failed");
        return new(1, job.Spec.Key,
            new(message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), job.ProviderId, job.Inputs),
            token, delivery.CallbackUrl);
    }

    public static bool ValidToken(AsyncDelivery delivery, string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 512) return false;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expected = Convert.FromHexString(delivery.TokenHash);
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
