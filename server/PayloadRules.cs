using System.Globalization;
using System.Text.RegularExpressions;

namespace Workflow;

public record PayloadCondition(string Field, string Operator, string Value);
public record PayloadRule(string Match, PayloadCondition[] Conditions);
public record PayloadRoute(string Key, string Label, string To, int Priority, PayloadRule When);
public record PayloadDecision(string To, string? RouteKey, string RouteLabel, int? Priority, string Reason);

public static class PayloadRules
{
    public static string[] Operators(string type) => type is "number" or "date"
        ? ["equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual", "exists"]
        : ["equals", "notEquals", "contains", "startsWith", "in", "exists"];

    public static void Validate(Definition definition, Transition transition)
    {
        var routes = transition.Routes ?? [];
        Engine.Require(routes.Length <= 25 && routes.Select(r => r?.Key).Distinct().Count() == routes.Length, "עד 25 מסלולי payload, עם מזהים ייחודיים לפעולה");
        foreach (var route in routes)
        {
            Engine.Require(route != null && Regex.IsMatch(route.Key ?? "", "^[a-z][a-zA-Z0-9_-]{0,59}$") && !string.IsNullOrWhiteSpace(route.Label) && route.Label.Length <= 120, "נדרשים מזהה ושם למסלול payload");
            Engine.Require(route!.Priority is >= 0 and <= 100000 && definition.States.Any(s => s.Key == route.To), "עדיפות או יעד מסלול payload אינם תקינים");
            var rule = route.When;
            Engine.Require(rule != null && rule.Match is "all" or "any" && rule.Conditions is { Length: > 0 and <= 25 }, "מסלול מותנה דורש 1–25 תנאים ובחירה של כל התנאים או לפחות אחד");
            foreach (var condition in rule!.Conditions)
            {
                var field = definition.Fields.SingleOrDefault(f => f.Key == condition?.Field);
                Engine.Require(field != null && condition != null && Operators(field.Type).Contains(condition.Operator) && condition.Value != null && condition.Value.Length <= 4000, "שדה או אופרטור שאינם תואמים לקטלוג ה־payload");
                if (condition!.Operator == "exists") Engine.Require(condition.Value is "true" or "false", "תנאי קיום דורש true או false");
                else
                {
                    Engine.Require(!string.IsNullOrWhiteSpace(condition.Value), "יש להזין ערך לתנאי ה־payload");
                    if (field!.Type is "number" or "date") Engine.ValidateValue(field, condition.Value);
                }
            }
        }
    }

    public static bool Matches(Field field, PayloadCondition condition, IReadOnlyDictionary<string, string> payload)
    {
        var present = payload.TryGetValue(field.Key, out var value) && !string.IsNullOrWhiteSpace(value);
        if (condition.Operator == "exists") return present == (condition.Value == "true");
        if (!present) return false;
        var actual = value!.Trim();
        var expected = condition.Value.Trim();
        int comparison;
        if (field.Type == "number")
        {
            if (!decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) || !decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)) return false;
            comparison = a.CompareTo(b);
        }
        else if (field.Type == "date")
        {
            if (!DateOnly.TryParseExact(actual, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) || !DateOnly.TryParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var b)) return false;
            comparison = a.CompareTo(b);
        }
        else comparison = string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
        return condition.Operator switch
        {
            "equals" => comparison == 0,
            "notEquals" => comparison != 0,
            "greaterThan" => comparison > 0,
            "greaterOrEqual" => comparison >= 0,
            "lessThan" => comparison < 0,
            "lessOrEqual" => comparison <= 0,
            "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "startsWith" => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            "in" => expected.Split(',').Any(v => string.Equals(actual, v.Trim(), StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    public static PayloadDecision Decide(Definition definition, Transition transition, IReadOnlyDictionary<string, string> payload)
    {
        foreach (var route in (transition.Routes ?? []).OrderBy(r => r.Priority).ThenBy(r => r.Key, StringComparer.Ordinal))
        {
            var matches = route.When.Conditions.Select(c => Matches(definition.Fields.Single(f => f.Key == c.Field), c, payload));
            if (route.When.Match == "all" ? matches.All(value => value) : matches.Any(value => value))
                return new(route.To, route.Key, route.Label, route.Priority, "המסלול הראשון שתנאיו מתקיימים לפי סדר העדיפות");
        }
        return new(transition.To, null, "ברירת מחדל", null, "לא התקיים תנאי למסלול מותנה; נבחר יעד ברירת המחדל של הפעולה");
    }

    public static void ValidatePayload(Definition definition, IReadOnlyDictionary<string, string> payload)
    {
        foreach (var (key, value) in payload)
        {
            var field = definition.Fields.SingleOrDefault(f => f.Key == key);
            Engine.Require(field != null, "שדה שאינו בקטלוג ה־payload: " + key);
            Engine.ValidateValue(field!, value);
        }
    }
}
