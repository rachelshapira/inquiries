using Microsoft.EntityFrameworkCore;

namespace Workflow;

public record UnitInput(string Key, string Name, string? ParentKey, long Version);
public record RuleInput(string Name, int Priority, bool Enabled, string TargetUnit, RoutingSpec Spec, long Version, string? ProcessKey = null);
public static class RoutingApi
{
    private static Account User(HttpContext c) => (Account)c.Items["account"]!;
    private static object View(RoutingRule r) => new { r.Id, r.ProcessKey, r.Name, r.Priority, r.Enabled, r.TargetUnit, r.Version, r.UpdatedAt, Spec = Json.Read<RoutingSpec>(r.SpecJson) };
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/org-units", async (WorkflowDb db, HttpContext c) =>
        {
            var u = User(c);
            return await db.OrgUnits.Where(x => u.Scope.Headquarters || u.Scope.Units.Contains(x.Key)).OrderBy(x => x.Key).ToListAsync();
        });
        api.MapPost("/org-units", async (UnitInput input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c));
            ValidateUnit(input);
            Engine.Require(input.ParentKey != null && await db.OrgUnits.AnyAsync(x => x.Key == input.ParentKey), "נדרשת יחידת אב קיימת");
            var unit = new OrgUnit { Key = input.Key, Name = input.Name.Trim(), ParentKey = input.ParentKey };
            db.OrgUnits.Add(unit); await db.SaveChangesAsync(); return Results.Ok(unit);
        });
        api.MapPut("/org-units/{key}", async (string key, UnitInput input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c));
            ValidateUnit(input); Engine.Require(input.Key == key, "לא ניתן לשנות מזהה יחידה");
            // Reading and changing the tree in one serializable transaction prevents concurrent cycles.
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var tree = await db.OrgUnits.ToListAsync();
            var unit = tree.SingleOrDefault(x => x.Key == key) ?? throw new RuleException("היחידה לא נמצאה", 404);
            Engine.Require(unit.Version == input.Version, "המבנה הארגוני עודכן במקביל. יש לרענן", 409);
            Engine.Require(key != OrgTree.Headquarters || input.ParentKey == null, "המטה חייב להישאר בשורש ההיררכיה");
            if (key != OrgTree.Headquarters)
                Engine.Require(input.ParentKey != null && tree.Any(x => x.Key == input.ParentKey) && !OrgTree.Subtree(tree, key).Contains(input.ParentKey), "יחידת האב חסרה או יוצרת מעגל בהיררכיה");
            unit.Name = input.Name.Trim(); unit.ParentKey = input.ParentKey; unit.Version++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(unit);
        });
        api.MapGet("/routing/rules", async (WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c));
            return (await db.RoutingRules.OrderBy(r => r.Priority).ThenBy(r => r.Id).ToListAsync()).Select(View);
        });
        api.MapPost("/routing/rules", async (RuleInput input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c)); await ValidateRule(input, db);
            var rule = new RoutingRule(); SetRule(rule, input); db.RoutingRules.Add(rule); await db.SaveChangesAsync(); return Results.Ok(View(rule));
        });
        api.MapPut("/routing/rules/{id:int}", async (int id, RuleInput input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c)); await ValidateRule(input, db);
            var rule = await db.RoutingRules.FindAsync(id) ?? throw new RuleException("הכלל לא נמצא", 404);
            Engine.Require(rule.Version == input.Version, "הכלל עודכן במקביל. יש לרענן", 409);
            SetRule(rule, input); rule.Version++; await db.SaveChangesAsync(); return Results.Ok(View(rule));
        });
        api.MapPost("/routing/simulate", async (NewCase input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c));
            var provider = await db.Providers.FindAsync(input.ProviderId) ?? throw new RuleException("נותן השירות לא נמצא", 404);
            var process = await db.Processes.FindAsync(input.ProcessVersionId) ?? throw new RuleException("התהליך לא נמצא", 404);
            Engine.Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 160, "נדרשת כותרת עד 160 תווים");
            var item = new Case { Provider = provider, ProcessVersion = process, State = Json.Read<Definition>(process.DefinitionJson).InitialState };
            Engine.UpdateFields(item, User(c), input.Data ?? []);
            return await RoutingEngine.Decide(db, provider, process.Key, input.Title.Trim(), Json.Read<Dictionary<string, string>>(item.DataJson));
        });
    }
    private static void ValidateUnit(UnitInput input) => Engine.Require(
        System.Text.RegularExpressions.Regex.IsMatch(input.Key ?? "", "^[a-z][a-z0-9-]{0,79}$") && !string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160,
        "נדרשים מזהה באנגלית ושם יחידה עד 160 תווים");
    private static async Task ValidateRule(RuleInput input, WorkflowDb db)
    {
        Engine.Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160 && input.Priority is >= 0 and <= 100000, "שם הכלל או העדיפות אינם תקינים");
        RoutingEngine.Validate(input.Spec);
        // Existing shared rules remain readable; every newly saved rule belongs to one process.
        Engine.Require(!string.IsNullOrWhiteSpace(input.ProcessKey), "יש לבחור תהליך לכלל הניתוב");
        var process = await db.Processes.Where(p => p.Key == input.ProcessKey).OrderByDescending(p => p.Number).FirstOrDefaultAsync();
        Engine.Require(process != null, "התהליך אינו קיים");
        var fields = Json.Read<Definition>(process!.DefinitionJson).Fields;
        foreach (var condition in input.Spec.Conditions.Where(c => c.Field.StartsWith("data.")))
            Engine.Require(fields.Any(f => f.Key == condition.Field[5..]), "השדה אינו מוגדר בטופס של התהליך");
        Engine.Require(await db.OrgUnits.AnyAsync(u => u.Key == input.TargetUnit), "יעד הניתוב חייב להיות יחידה קיימת");
    }
    private static void SetRule(RoutingRule rule, RuleInput input)
    {
        rule.ProcessKey = input.ProcessKey; rule.Name = input.Name.Trim(); rule.Priority = input.Priority; rule.Enabled = input.Enabled; rule.TargetUnit = input.TargetUnit;
        rule.SpecJson = Json.Write(input.Spec); rule.UpdatedAt = DateTime.UtcNow;
    }
}
