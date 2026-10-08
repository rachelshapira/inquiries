using Microsoft.EntityFrameworkCore;

namespace Workflow;

public static class Seed
{
    public static Definition Pilot(string name, string document) => new(name, "draft",
        [new Field("contact", "איש קשר לפנייה", "text", true, Engine.Roles, ["Provider", "Admin"], ["draft", "corrections"]),
         new Field("email", "דואר אלקטרוני", "email", true, Engine.Roles, ["Provider", "Admin"], ["draft", "corrections"]),
         new Field("notes", "פרטים נוספים", "textarea", false, Engine.Roles, ["Provider", "Admin"], ["draft", "corrections"])],
        [new State("draft", "טיוטה", false), new State("review", "בבדיקה", false), new State("corrections", "ממתין להשלמות", false), new State("approval", "ממתין לאישור", false), new State("approved", "אושרה", true), new State("cancelled", "בוטלה", true)],
        [new Transition("submit", "הגשת הפנייה", "draft", "review", ["Provider", "Admin"], "submission", ["newRound"]),
         new Transition("correct", "בקשת השלמות", "review", "corrections", ["Reviewer", "Admin"], "reason", ["closeTasks"]),
         new Transition("resubmit", "הגשה מחדש", "corrections", "review", ["Provider", "Admin"], "submission", ["newRound"]),
         new Transition("recommend", "העברה לאישור", "review", "approval", ["Reviewer", "Admin"], "reviewsPassed", []),
         new Transition("approve", "אישור הפנייה", "approval", "approved", ["Approver", "Admin"], "reviewsPassed", ["sendMail"]),
         new Transition("return", "החזרה להשלמות", "approval", "corrections", ["Approver", "Admin"], "reason", ["closeTasks"]),
         new Transition("cancel", "ביטול הפנייה", "draft", "cancelled", ["Provider", "Admin"], "reason", ["closeTasks"]),
         new Transition("cancel", "ביטול הפנייה", "review", "cancelled", ["Admin"], "reason", ["closeTasks"]),
         new Transition("cancel", "ביטול הפנייה", "corrections", "cancelled", ["Provider", "Admin"], "reason", ["closeTasks"])], [document]);

    public static async Task Initialize(WorkflowDb db)
    {
        if (!await db.OrgUnits.AnyAsync())
        {
            db.OrgUnits.AddRange(
                new OrgUnit { Key = "hq", Name = "מטה ארצי" },
                new OrgUnit { Key = "care", Name = "מחוז מרכז", ParentKey = "hq" },
                new OrgUnit { Key = "care-local", Name = "סניף ירושלים", ParentKey = "care" },
                new OrgUnit { Key = "north", Name = "מחוז צפון", ParentKey = "hq" },
                new OrgUnit { Key = "north-haifa", Name = "סניף חיפה", ParentKey = "north" });
            await db.SaveChangesAsync();
        }
        if (await db.Accounts.AnyAsync())
        {
            var admin = await db.Accounts.FindAsync("admin");
            if (admin != null && admin.Unit == "care") admin.Unit = "hq";
            await EnsureBranchUsers(db);
            await db.SaveChangesAsync();
            return;
        }
        var p = new Provider { Name = "אופק שירותי סיעוד", Registration = "515001234", Unit = "care", Branches = "ירושלים · תל אביב", ContactName = "נועה לוי", Email = "noa@example.org", Phone = "02-5550100", Agreement = "הסכם שירות לשנת 2026 • עדכון ידני" };
        var other = new Provider { Name = "בית טוב בקהילה", Registration = "515009876", Unit = "north", Branches = "חיפה", ContactName = "דניאל כהן", Email = "daniel@example.org" };
        db.Providers.AddRange(p, other);
        var process = new ProcessVersion { Key = "document-submission", Number = 1, DefinitionJson = Json.Write(Pilot("הגשת מסמכים", "אישור ביטוח")) };
        var second = new ProcessVersion { Key = "agreement-renewal", Number = 1, DefinitionJson = Json.Write(Pilot("חידוש הסכם", "הסכם חתום")) };
        db.Processes.AddRange(process, second); await db.SaveChangesAsync();
        db.Accounts.AddRange(
            new Account { Id = "admin", Name = "מנהל המטה", Role = "Admin", Unit = "hq" },
            new Account { Id = "provider", Name = "נועה לוי", Role = "Provider", ProviderId = p.Id },
            new Account { Id = "reviewer", Name = "יעל ישראלי", Role = "Reviewer" },
            new Account { Id = "approver", Name = "איתי ברק", Role = "Approver" },
            new Account { Id = "other-provider", Name = "דניאל כהן", Role = "Provider", ProviderId = other.Id, Unit = "north" });
        db.Cases.Add(new Case { Provider = p, ProcessVersion = process, Title = "הגשת אישור ביטוח לשנת 2026", DataJson = Json.Write(new { contact = "נועה לוי", email = "noa@example.org", notes = "" }), History = [new History { Actor = "מנהל המערכת", Action = "פתיחת פנייה" }] });
        db.Cases.Add(new Case { Provider = other, ProcessVersion = second, Title = "חידוש הסכם שירות", Unit = "north", History = [new History { Actor = "מנהל המערכת", Action = "פתיחת פנייה" }] });
        await db.SaveChangesAsync();
        await EnsureBranchUsers(db);
        await db.SaveChangesAsync();
    }
    private static async Task EnsureBranchUsers(WorkflowDb db)
    {
        var demos = new[] {
            new Account { Id = "north-reviewer", Name = "בודק מחוז צפון", Role = "Reviewer", Unit = "north" },
            new Account { Id = "haifa-reviewer", Name = "בודק סניף חיפה", Role = "Reviewer", Unit = "north-haifa" },
            new Account { Id = "hq-reviewer", Name = "בודק מטה ארצי", Role = "Reviewer", Unit = "hq" },
            new Account { Id = "branch-admin", Name = "מנהל סניף ירושלים", Role = "Admin", Unit = "care-local" }
        };
        foreach (var account in demos) if (!await db.Accounts.AnyAsync(a => a.Id == account.Id)) db.Accounts.Add(account);
    }
}


