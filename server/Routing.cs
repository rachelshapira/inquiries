using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Workflow;

public class OrgUnit
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ParentKey { get; set; }
    public long Version { get; set; } = 1;
}
public record OrgScope(bool Headquarters, string[] Units)
{
    public bool Contains(string unit) => Headquarters || Units.Contains(unit, StringComparer.Ordinal);
}
public static class OrgTree
{
    public const string Headquarters = "hq";
    public static string[] Subtree(IEnumerable<OrgUnit> tree, string root)
    {
        var nodes = tree.ToArray();
        if (!nodes.Any(u => u.Key == root)) return [];
        var keys = new HashSet<string>(StringComparer.Ordinal) { root };
        // ponytail: load the small org tree per request; use a closure table for very large hierarchies.
        while (true)
        {
            var count = keys.Count;
            foreach (var node in nodes.Where(n => n.ParentKey != null && keys.Contains(n.ParentKey))) keys.Add(node.Key);
            if (keys.Count == count) return keys.ToArray();
        }
    }
    public static async Task<OrgScope> Scope(WorkflowDb db, string unit, string access = "subtree")
    {
        var tree = await db.OrgUnits.AsNoTracking().ToListAsync();
        return EffectiveScope(tree, unit, access);
    }
    public static OrgScope EffectiveScope(IEnumerable<OrgUnit> tree, string unit, string access) =>
        access is not ("own" or "subtree") ? new(false, []) : access == "own" ? new(false, tree.Any(u => u.Key == unit) ? [unit] : []) :
        new(unit == Headquarters && tree.Any(u => u.Key == Headquarters && u.ParentKey == null), Subtree(tree, unit));
    public static void Manage(Account user) => Engine.Require(user.Role == "Admin" && user.Scope.Headquarters && user.WriteScope.Headquarters, "ניהול המבנה הארגוני וכללי הניתוב מחייב מנהל מטה", 403);
}
public record RoutingCondition(string Field, string Operator, string Value);
public record RoutingSpec(string Match, RoutingCondition[] Conditions);
public class RoutingRule
{
    public string? ProcessKey { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public string TargetUnit { get; set; } = "";
    public string SpecJson { get; set; } = "{}";
    public long Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class RoutingDecision
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int? RuleId { get; set; }
    public long? RuleVersion { get; set; }
    public string RuleName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
public record RouteResult(string Unit, string UnitName, int? RuleId, long? RuleVersion, string RuleName, string Reason);
public static class RoutingEngine
{
    public static readonly string[] Operators = ["equals", "notEquals", "contains", "startsWith", "in", "exists", "greaterThan", "lessThan"];
    public static void Validate(RoutingSpec spec)
    {
        Engine.Require(spec != null && spec.Match is "all" or "any" && spec.Conditions is { Length: <= 25 }, "יש לבחור התאמה לכל התנאים או לתנאי אחד, עד 25 תנאים");
        Engine.Require(spec!.Match != "any" || spec.Conditions.Length > 0, "התאמה לתנאי אחד דורשת לפחות תנאי אחד");
        foreach (var c in spec.Conditions)
        {
            Engine.Require(c != null && c.Field != null && (new[] { "processKey", "providerId", "providerUnit", "registration", "title" }.Contains(c.Field) || Regex.IsMatch(c.Field, @"^data\.[a-z][a-zA-Z0-9_-]{0,59}$")), "שדה ניתוב לא תקין");
            Engine.Require(Operators.Contains(c!.Operator) && c.Value != null && c.Value.Length <= 4000, "תנאי ניתוב לא תקין");
            Engine.Require(c.Operator != "exists" || c.Value is "true" or "false", "ערך לתנאי קיום חייב להיות true או false");
            Engine.Require(c.Operator is not ("greaterThan" or "lessThan") || decimal.TryParse(c.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _), "השוואה מספרית מחייבת מספר תקין");
        }
    }
    public static bool Matches(RoutingCondition c, IReadOnlyDictionary<string, string> values)
    {
        var present = values.TryGetValue(c.Field, out var value) && !string.IsNullOrWhiteSpace(value);
        if (c.Operator == "exists") return present == (c.Value == "true");
        if (!present) return false; // Missing fields never satisfy negative or numeric comparisons.
        var expected = c.Value.Trim();
        var text = value!.Trim();
        return c.Operator switch
        {
            "equals" => string.Equals(text, expected, StringComparison.OrdinalIgnoreCase),
            "notEquals" => !string.Equals(text, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => text.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "startsWith" => text.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            "in" => expected.Split(',').Any(s => string.Equals(text, s.Trim(), StringComparison.OrdinalIgnoreCase)),
            "greaterThan" or "lessThan" => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var b) && (c.Operator == "greaterThan" ? a > b : a < b),
            _ => false
        };
    }
    public static RouteResult Decide(IEnumerable<RoutingRule> rules, IEnumerable<OrgUnit> units, Provider provider, string processKey, string title, IReadOnlyDictionary<string, string> data)
    {
        var tree = units.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["providerId"] = provider.Id.ToString(CultureInfo.InvariantCulture), ["providerUnit"] = provider.Unit, ["registration"] = provider.Registration, ["processKey"] = processKey, ["title"] = title };
        foreach (var pair in data) values["data." + pair.Key] = pair.Value;
        foreach (var rule in rules.Where(r => r.Enabled && (r.ProcessKey == null || r.ProcessKey == processKey)).OrderBy(r => r.Priority).ThenBy(r => r.Id))
        {
            var spec = Json.Read<RoutingSpec>(rule.SpecJson);
            Validate(spec);
            var matches = spec.Match == "all" ? spec.Conditions.All(c => Matches(c, values)) : spec.Conditions.Any(c => Matches(c, values));
            if (!matches) continue;
            Engine.Require(tree.ContainsKey(rule.TargetUnit), "יעד כלל הניתוב אינו קיים. יש לפנות למנהל המערכת", 409);
            return new(rule.TargetUnit, tree[rule.TargetUnit].Name, rule.Id, rule.Version, rule.Name, "הכלל הפעיל הראשון לפי סדר העדיפות");
        }
        Engine.Require(tree.ContainsKey(provider.Unit), "לא נמצא כלל מתאים ויחידת ברירת המחדל של נותן השירות אינה מוגדרת", 409);
        return new(provider.Unit, tree[provider.Unit].Name, null, null, "ברירת מחדל", "לא נמצא כלל מתאים; נבחרה יחידת נותן השירות");
    }
    public static async Task<RouteResult> Decide(WorkflowDb db, Provider provider, string processKey, string title, IReadOnlyDictionary<string, string> data) =>
        Decide(await db.RoutingRules.AsNoTracking().ToListAsync(), await db.OrgUnits.AsNoTracking().ToListAsync(), provider, processKey, title, data);
    public static void Apply(Case item, RouteResult route)
    {
        item.Unit = route.Unit;
        item.Routing = new RoutingDecision { Unit = route.Unit, UnitName = route.UnitName, RuleId = route.RuleId, RuleVersion = route.RuleVersion, RuleName = route.RuleName, Reason = route.Reason };
    }
}
