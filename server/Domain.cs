using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Workflow;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}

public class Account
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "Provider";
    public string Unit { get; set; } = "care";
    public int? ProviderId { get; set; }
    public bool Active { get; set; } = true;
    public string ReadAccess { get; set; } = "subtree";
    public string WriteAccess { get; set; } = "subtree";
    public long Version { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, System.Text.Json.Serialization.JsonIgnore]
    public OrgScope WriteScope { get; set; } = new(false, []);
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, System.Text.Json.Serialization.JsonIgnore]
    public OrgScope Scope { get; set; } = new(false, []);
}
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Registration { get; set; } = "";
    public string Unit { get; set; } = "care";
    public string Branches { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Agreement { get; set; } = "";
    public long Version { get; set; } = 1;
}
public record Field(string Key, string Label, string Type, bool Required, string[] ViewRoles, string[] EditRoles, string[] EditStates);
public record State(string Key, string Label, bool Terminal);
public record BusinessActionSpec(string Key, Dictionary<string, string> Inputs, string Success, string Failure);
public record Transition(string Key, string Label, string From, string To, string[] Roles, string Guard, string[] Effects, PayloadRoute[]? Routes = null, BusinessActionSpec? BusinessAction = null, string Trigger = "user");
public record Definition(string Name, string InitialState, Field[] Fields, State[] States, Transition[] Transitions, string[] Documents);
public class ProcessVersion
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public int Number { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
public class Case
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public Provider Provider { get; set; } = null!;
    public int ProcessVersionId { get; set; }
    public ProcessVersion ProcessVersion { get; set; } = null!;
    public string Title { get; set; } = "";
    public string State { get; set; } = "draft";
    public string Unit { get; set; } = "care";
    public string? AssigneeId { get; set; }
    public string DataJson { get; set; } = "{}";
    public int Round { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Document> Documents { get; set; } = [];
    public List<ReviewTask> Tasks { get; set; } = [];
    public List<History> History { get; set; } = [];
    public RoutingDecision? Routing { get; set; }
}
public class Document
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Kind { get; set; } = "";
    public int Number { get; set; }
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public long Size { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
public class ReviewTask
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int DocumentId { get; set; }
    public int Round { get; set; }
    public string Status { get; set; } = "open";
    public string? Result { get; set; }
    public string? ReviewerId { get; set; }
    public string Note { get; set; } = "";
    public DateTime DueAt { get; set; } = DateTime.UtcNow.AddDays(3);
}
public class History
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public bool IsPublic { get; set; }
    public string? PublicNote { get; set; }
    public int Round { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
public class Outbox
{
    public long Id { get; set; }
    public int CaseId { get; set; }
    public string Kind { get; set; } = "notification";
    public string Recipient { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttempt { get; set; } = DateTime.UtcNow;
    public string? ClaimId { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public long Version { get; set; } = 1;
}
public class Notification
{
    public long Id { get; set; }
    public long OutboxId { get; set; }
    public int CaseId { get; set; }
    public string Message { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
public class WorkflowDb(DbContextOptions<WorkflowDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProcessVersion> Processes => Set<ProcessVersion>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ReviewTask> Tasks => Set<ReviewTask>();
    public DbSet<History> History => Set<History>();
    public DbSet<Outbox> Outbox => Set<Outbox>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<RoutingDecision> RoutingDecisions => Set<RoutingDecision>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>().Property(x => x.Id).HasMaxLength(200);
        b.Entity<Account>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Account>().Property(x => x.ReadAccess).HasMaxLength(16);
        b.Entity<Account>().Property(x => x.WriteAccess).HasMaxLength(16);
        b.Entity<Provider>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Provider>().Property(x => x.Registration).HasMaxLength(32);
        b.Entity<Provider>().HasIndex(x => x.Registration).IsUnique();
        b.Entity<Case>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Case>().HasIndex(x => new { x.Unit, x.State, x.UpdatedAt });
        b.Entity<Case>().HasOne(x => x.Provider).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<Case>().HasOne(x => x.ProcessVersion).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProcessVersion>().Property(x => x.Key).HasMaxLength(80);
        b.Entity<ProcessVersion>().HasIndex(x => new { x.Key, x.Number }).IsUnique();
        b.Entity<Document>().Property(x => x.Kind).HasMaxLength(100);
        b.Entity<Document>().HasIndex(x => new { x.CaseId, x.Kind, x.Number }).IsUnique();
        b.Entity<ReviewTask>().HasIndex(x => new { x.CaseId, x.Round, x.DocumentId }).IsUnique();
        b.Entity<Outbox>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Outbox>().HasIndex(x => new { x.Status, x.NextAttempt });
        b.Entity<Notification>().HasIndex(x => x.OutboxId).IsUnique();
        b.Entity<OrgUnit>().HasKey(x => x.Key);
        b.Entity<OrgUnit>().Property(x => x.Key).HasMaxLength(80);
        b.Entity<OrgUnit>().Property(x => x.ParentKey).HasMaxLength(80);
        b.Entity<OrgUnit>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<OrgUnit>().HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentKey).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoutingRule>().Property(x => x.ProcessKey).HasMaxLength(80);
        b.Entity<RoutingRule>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<RoutingRule>().HasIndex(x => new { x.Enabled, x.Priority });
        b.Entity<RoutingRule>().Property(x => x.TargetUnit).HasMaxLength(80);
        b.Entity<RoutingRule>().HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.TargetUnit).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoutingDecision>().HasOne<Case>().WithOne(x => x.Routing).HasForeignKey<RoutingDecision>(x => x.CaseId);
    }
}
public sealed class RuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
