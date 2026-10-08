using Microsoft.EntityFrameworkCore;
namespace Workflow;
public record AccountInput(string Id, string Name, string Role, string Unit, int? ProviderId, bool Active, string ReadAccess, string WriteAccess, long Version);
public static class OrganizationApi
{
    private static Account User(HttpContext c) => (Account)c.Items["account"]!;
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/organization/accounts", async (WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c));
            return await db.Accounts.OrderBy(a => a.Name).ToListAsync();
        });
        api.MapPost("/organization/accounts", async (AccountInput input, WorkflowDb db, HttpContext c) =>
        {
            OrgTree.Manage(User(c)); await Validate(input, db);
            Engine.Require(!await db.Accounts.AnyAsync(a => a.Id == input.Id), "המזהה כבר משויך למשתמש");
            var account = new Account { Id = input.Id }; Set(account, input);
            db.Accounts.Add(account); await db.SaveChangesAsync(); return Results.Ok(account);
        });
        api.MapPut("/organization/accounts/{id}", async (string id, AccountInput input, WorkflowDb db, HttpContext c) =>
        {
            var manager = User(c); OrgTree.Manage(manager); await Validate(input, db);
            Engine.Require(input.Id == id, "לא ניתן לשנות מזהה משתמש");
            var account = await db.Accounts.FindAsync(id) ?? throw new RuleException("המשתמש לא נמצא", 404);
            Engine.Require(account.Version == input.Version, "הרשאות המשתמש עודכנו במקביל. יש לרענן", 409);
            if (id == manager.Id) Engine.Require(input.Active && input.Role == "Admin" && input.Unit == "hq" && input.ReadAccess == "subtree" && input.WriteAccess == "subtree", "לא ניתן להסיר מעצמך את הגישה לניהול הארגון");
            Set(account, input); account.Version++; await db.SaveChangesAsync(); return Results.Ok(account);
        });
    }
    private static async Task Validate(AccountInput input, WorkflowDb db)
    {
        Engine.Require(!string.IsNullOrWhiteSpace(input.Id) && input.Id.Length <= 200 && !string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160 && Engine.Roles.Contains(input.Role), "נדרשים מזהה, שם ותפקיד תקינים");
        Engine.Require(new[] { "none", "own", "subtree" }.Contains(input.ReadAccess) && new[] { "none", "own", "subtree" }.Contains(input.WriteAccess), "הרשאות לא תקינות");
        Engine.Require(input.WriteAccess == "none" || input.ReadAccess == "subtree" || input.ReadAccess == input.WriteAccess, "כתיבה חייבת להיות בתחום הרשאת הקריאה");
        Engine.Require(await db.OrgUnits.AnyAsync(u => u.Key == input.Unit), "יש לבחור יחידה קיימת");
        Engine.Require(input.Role != "Provider" || input.ProviderId != null && await db.Providers.AnyAsync(p => p.Id == input.ProviderId && p.Unit == input.Unit), "נותן שירות חייב להיות משויך לרשומה באותה יחידה");
    }
    private static void Set(Account account, AccountInput input)
    {
        account.Name = input.Name.Trim(); account.Role = input.Role; account.Unit = input.Unit; account.ProviderId = input.Role == "Provider" ? input.ProviderId : null;
        account.Active = input.Active; account.ReadAccess = input.ReadAccess; account.WriteAccess = input.WriteAccess;
    }
}
