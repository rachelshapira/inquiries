using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Workflow;

public static partial class Engine
{
    public static readonly string[] Roles = ["Provider", "Reviewer", "Approver", "Admin"];
    public static readonly string[] Guards = ["none", "submission", "reason", "reviewsPassed"];
    public static readonly string[] Effects = ["none", "newRound", "closeTasks", "sendMail"];
    [GeneratedRegex("^[a-z][a-zA-Z0-9_-]{0,59}$")]
    private static partial Regex KeyPattern();
    public static Definition Definition(Case item) => Json.Read<Definition>(item.ProcessVersion.DefinitionJson);
    public static bool CanAccess(Account user, Case item) => user.ReadAccess != "none" && (user.Role == "Provider"
        ? user.ProviderId == item.ProviderId : user.Scope.Contains(item.Unit));
    public static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message, int status = 400)
    {
        if (!condition) throw new RuleException(message, status);
    }
    public static void Access(Account user, Case item) => Require(CanAccess(user, item), "אין הרשאה לפנייה זו", 403);
    public static bool CanWrite(Account user, Case item) => CanAccess(user, item) && user.WriteAccess != "none" && (user.Role == "Provider" ? user.ProviderId == item.ProviderId : user.WriteScope.Contains(item.Unit));
    public static void WriteAccess(Account user, Case item) => Require(CanWrite(user, item), "אין הרשאת כתיבה לפנייה ביחידה זו", 403);
    public static void Version(Case item, long version) => Require(item.Version == version, "הפנייה עודכנה בידי משתמש אחר. יש לרענן ולנסות שוב", 409);
    public static void Touch(Case item, Account user, string action, string note = "", bool isPublic = false, string? publicNote = null)
    {
        item.Version++;
        item.UpdatedAt = DateTime.UtcNow;
        item.History.Add(new History { Actor = user.Name, Action = action, Note = note, Round = item.Round, IsPublic = isPublic, PublicNote = isPublic ? publicNote : null });
    }
    public static bool CanEdit(Field field, Case item, Account user) => CanWrite(user, item) && field.ViewRoles.Contains(user.Role) && field.EditRoles.Contains(user.Role) && field.EditStates.Contains(item.State);
    public static bool CanUpload(Case item, Account user) => CanWrite(user, item) && Definition(item).Transitions.Any(t => t.From == item.State && t.Guard == "submission" && t.Roles.Contains(user.Role));
    // Administrators configure the platform; business roles initiate using existing permissions.
    public static bool CanInitiateProcess(Account user, ProcessVersion process, Provider provider)
    {
        var definition = Json.Read<Definition>(process.DefinitionJson);
        return user.Role != "Admin" && user.Active && CanWrite(user, new Case { ProviderId = provider.Id, Unit = provider.Unit }) &&
            definition.Transitions.Any(t => t.From == definition.InitialState && t.Trigger == "user" && t.Roles.Contains(user.Role));
    }
    public static bool ValidDocument(Document document) => document.ValidUntil == null || document.ValidUntil.Value.Date >= DateTime.UtcNow.Date;
    public static Document[] CurrentDocuments(Case item) => item.Documents.GroupBy(d => d.Kind).Select(g => g.MaxBy(d => d.Number)!).ToArray();
    public static void UpdateFields(Case item, Account user, Dictionary<string, string> changes)
    {
        var definition = Definition(item);
        var data = Json.Read<Dictionary<string, string>>(item.DataJson);
        foreach (var (key, value) in changes)
        {
            var field = definition.Fields.SingleOrDefault(f => f.Key == key);
            Require(field != null && CanEdit(field, item, user), "אין הרשאת עריכה לשדה: " + key, 403);
            ValidateValue(field!, value);
            data[key] = value.Trim();
        }
        item.DataJson = Json.Write(data);
    }
    public static void ValidateValue(Field field, string value)
    {
        Require(value != null && value.Length <= 4000, "ערך ארוך מדי: " + field.Label);
        if (string.IsNullOrWhiteSpace(value)) return;
        Require(field.Type != "number" || decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _), "נדרש מספר: " + field.Label);
        Require(field.Type != "date" || DateOnly.TryParseExact(value, "yyyy-MM-dd", out _), "נדרש תאריך תקין: " + field.Label);
        Require(field.Type != "email" || System.Net.Mail.MailAddress.TryCreate(value, out _), "כתובת דואר לא תקינה: " + field.Label);
    }
    public static string? GuardError(Case item, Transition transition, string note)
    {
        if (transition.Guard == "reason" && string.IsNullOrWhiteSpace(note)) return "יש להזין את פירוט ההשלמות או סיבת הפעולה";
        if (transition.Guard == "submission")
        {
            var data = Json.Read<Dictionary<string, string>>(item.DataJson);
            foreach (var f in Definition(item).Fields)
                if (f.Required && (!data.TryGetValue(f.Key, out var value) || string.IsNullOrWhiteSpace(value))) return "שדה חובה חסר: " + f.Label;
            foreach (var kind in Definition(item).Documents)
                if (!CurrentDocuments(item).Any(d => d.Kind == kind && ValidDocument(d))) return "חסר מסמך בתוקף: " + kind;
        }
        if (transition.Guard == "reviewsPassed")
        {
            var docs = CurrentDocuments(item);
            if (item.Round == 0 || docs.Length == 0) return "לא בוצע סבב בדיקה";
            if (Definition(item).Documents.Any(kind => !docs.Any(d => d.Kind == kind))) return "חסר מסמך נדרש";
            if (docs.Any(d => d.ValidUntil != null && d.ValidUntil.Value.Date < DateTime.UtcNow.Date)) return "קיים מסמך שפג תוקפו";
            if (docs.Any(d => !item.Tasks.Any(t => t.DocumentId == d.Id && t.Round == item.Round && t.Status == "completed" && t.Result == "passed"))) return "כל המסמכים בגרסתם הנוכחית חייבים לעבור בדיקה בסבב הנוכחי";
            if (item.Tasks.Any(t => t.Round == item.Round && (t.Status != "completed" || t.Result != "passed"))) return "נותרו משימות שלא עברו בהצלחה";
        }
        return null;
    }
    public static Transition Execute(Case item, Account user, string action, string note, bool shareNote = false, string trigger = "user")
    {
        Access(user, item);
        Require(note.Length <= 4000, "ההערה ארוכה מדי");
        var transition = Definition(item).Transitions.SingleOrDefault(t => t.Key == action && t.From == item.State);
        Require(transition != null && transition.Trigger == trigger && (trigger != "user" || transition.Roles.Contains(user.Role)), "הפעולה אינה מותרת במצב הנוכחי או בתפקידך", 403);
        var definition = Definition(item);
        var decision = PayloadRules.Decide(definition, transition!, Json.Read<Dictionary<string, string>>(item.DataJson));
        var error = GuardError(item, transition!, note);
        Require(error == null, error ?? "");
        if (transition!.Effects.Contains("newRound"))
        {
            foreach (var task in item.Tasks.Where(t => t.Status == "open")) task.Status = "superseded";
            item.Round++;
            foreach (var doc in CurrentDocuments(item)) item.Tasks.Add(new ReviewTask { DocumentId = doc.Id, Round = item.Round });
        }
        if (transition.Effects.Contains("closeTasks"))
            foreach (var task in item.Tasks.Where(t => t.Status == "open")) task.Status = "superseded";
        var previousState = item.State;
        item.State = decision.To;
        var decisionNote = (transition.Routes?.Length ?? 0) == 0 ? "" : $"החלטת payload: {definition.States.Single(s => s.Key == previousState).Label} ← {definition.States.Single(s => s.Key == decision.To).Label}; מסלול: {decision.RouteLabel} ({decision.RouteKey ?? "default"}); עדיפות: {decision.Priority?.ToString() ?? "ברירת מחדל"}; נתוני פנייה בגרסה {item.Version}";
        // Business progress is public; notes are shared explicitly. Routing diagnostics stay internal.
        Touch(item, user, transition.Label, string.Join("\n", new[] { note, decisionNote }.Where(value => !string.IsNullOrWhiteSpace(value))), true,
            shareNote || user.Role == "Provider" ? note : null);
        return transition;
    }
    public static void ValidateDefinition(Definition definition)
    {
        Require(!string.IsNullOrWhiteSpace(definition.Name) && definition.Name.Length <= 120, "נדרש שם תהליך עד 120 תווים");
        Require(definition.States is { Length: > 1 and <= 30 } && definition.Fields is { Length: <= 50 } && definition.Transitions is { Length: > 0 and <= 100 } && definition.Documents is { Length: <= 15 }, "הגדרה חסרה או גדולה מדי");
        var states = definition.States.Select(s => s.Key).ToHashSet();
        Require(states.Count == definition.States.Length && states.All(k => k != null && KeyPattern().IsMatch(k)), "מזהי מצבים לא תקינים או כפולים");
        Require(states.Contains(definition.InitialState) && definition.States.Any(s => s.Terminal), "נדרשים מצב התחלתי ומצב סופי");
        Require(definition.States.All(s => !string.IsNullOrWhiteSpace(s.Label)) && definition.Transitions.All(t => !string.IsNullOrWhiteSpace(t.Label)), "יש לתת תווית לכל מצב ופעולה");
        Require(definition.Fields.Select(f => f.Key).Distinct().Count() == definition.Fields.Length, "מזהי שדות כפולים");
        foreach (var f in definition.Fields)
        {
            Require(f.Key != null && KeyPattern().IsMatch(f.Key) && !string.IsNullOrWhiteSpace(f.Label) && new[] { "text", "textarea", "number", "date", "email" }.Contains(f.Type), "הגדרת שדה לא תקינה");
            Require(f.ViewRoles != null && f.EditRoles != null && f.EditStates != null && f.ViewRoles.All(Roles.Contains) && f.EditRoles.All(r => f.ViewRoles.Contains(r)) && f.EditStates.All(states.Contains), "הרשאות שדה לא תקינות");
        }
        Require(definition.Documents.Distinct().Count() == definition.Documents.Length && definition.Documents.All(d => !string.IsNullOrWhiteSpace(d) && d.Length <= 100), "סוגי מסמכים לא תקינים");
        Require(definition.Transitions.Select(t => (t.From, t.Key)).Distinct().Count() == definition.Transitions.Length, "פעולה כפולה באותו מצב");
        foreach (var t in definition.Transitions)
        {
            Require(t.Key != null && KeyPattern().IsMatch(t.Key) && states.Contains(t.From) && states.Contains(t.To), "מעבר מפנה למצב חסר או מכיל מזהה לא תקין");
            Require(t.Roles is { Length: > 0 } && t.Roles.All(Roles.Contains) && Guards.Contains(t.Guard) && t.Effects != null && t.Effects.All(Effects.Contains) && t.Effects.Distinct().Count() == t.Effects.Length, "תפקיד, תנאי או פעולה שאינם בקטלוג");
            Require(!definition.States.Single(s => s.Key == t.From).Terminal, "אין להגדיר יציאה ממצב סופי");
            Require(!t.Effects.Contains("newRound") || t.Guard == "submission", "סבב חדש חייב לבדוק דרישות הגשה");
            Require(t.Guard != "submission" || t.Effects.Contains("newRound"), "הגשה חייבת ליצור סבב בדיקה");
            Require(!(t.Effects.Contains("newRound") && t.Effects.Contains("closeTasks")), "סבב חדש וסגירת משימות אינם יכולים לפעול יחד");
            Require(new[] { "user", "businessSuccess", "businessFailure" }.Contains(t.Trigger), "גורם הפעלת המעבר אינו תקין");
            if (t.BusinessAction is { } action)
            {
                Require(t.Trigger == "user" && (t.Routes?.Length ?? 0) == 0 && !definition.States.Single(s => s.Key == t.To).Terminal, "פעולה עסקית חייבת להמתין לתוצאה במצב שאינו סופי");
                var outgoing = definition.Transitions.Where(next => next.From == t.To).ToArray();
                Require(outgoing.Length == 2 && outgoing.Any(next => next.Key == action.Success && next.Trigger == "businessSuccess") && outgoing.Any(next => next.Key == action.Failure && next.Trigger == "businessFailure"), "נדרשים שני מעברי תוצאה אוטומטיים: הצלחה וכישלון");
                Require(!definition.Fields.Any(f => f.EditStates.Contains(t.To)), "אין לערוך קלט במצב הממתין לפעולה עסקית");
            }
            if (t.Trigger != "user")
                Require(t.Guard == "none" && t.BusinessAction == null && (t.Routes?.Length ?? 0) == 0 && !t.Effects.Contains("newRound") && definition.Transitions.Any(origin => origin.BusinessAction != null && origin.To == t.From && (origin.BusinessAction.Success == t.Key && t.Trigger == "businessSuccess" || origin.BusinessAction.Failure == t.Key && t.Trigger == "businessFailure")), "מעבר תוצאה חייב להיות מקושר לפעולה עסקית וללא תנאי ידני");
            PayloadRules.Validate(definition, t);
        }
        var reachable = new HashSet<string> { definition.InitialState };
        while (true)
        {
            var count = reachable.Count;
            foreach (var t in definition.Transitions.Where(t => reachable.Contains(t.From)))
            {
                reachable.Add(t.To);
                foreach (var route in t.Routes ?? []) reachable.Add(route.To);
            }
            if (count == reachable.Count) break;
        }
        Require(reachable.SetEquals(states), "קיימים מצבים שלא ניתן להגיע אליהם");
    }
    public static IQueryable<Case> Visible(WorkflowDb db, Account user) => user.ReadAccess == "none" ? db.Cases.Where(c => false) : user.Role == "Provider"
        ? db.Cases.Where(c => c.ProviderId == user.ProviderId)
        : user.Scope.Headquarters ? db.Cases : db.Cases.Where(c => user.Scope.Units.Contains(c.Unit));
    public static async Task<Case> Load(WorkflowDb db, int id, Account user)
    {
        var item = await db.Cases.Include(c => c.Provider).Include(c => c.ProcessVersion).Include(c => c.Documents).Include(c => c.Tasks).Include(c => c.History).Include(c => c.Routing).AsSplitQuery().SingleOrDefaultAsync(c => c.Id == id);
        Require(item != null, "הפנייה לא נמצאה", 404);
        Access(user, item!);
        return item!;
    }
    public static object View(Case c, Account user)
    {
        var definition = Definition(c);
        var external = user.Role == "Provider";
        var closingActions = definition.Transitions.Where(t => t.To == c.State || (t.Routes ?? []).Any(r => r.To == c.State)).Select(t => t.Label).ToHashSet();
        var response = c.History.Where(h => h.IsPublic && !string.IsNullOrWhiteSpace(h.PublicNote) && closingActions.Contains(h.Action)).OrderByDescending(h => h.At).FirstOrDefault();
        var fields = definition.Fields.Where(f => f.ViewRoles.Contains(user.Role)).ToArray();
        var data = Json.Read<Dictionary<string, string>>(c.DataJson).Where(pair => fields.Any(f => f.Key == pair.Key)).ToDictionary();
        return new { c.Id, c.Title, c.ProviderId, ProviderName = c.Provider.Name, c.State, StateLabel = definition.States.Single(s => s.Key == c.State).Label,
            c.Unit, Routing = external ? null : c.Routing, AssigneeId = external ? null : c.AssigneeId, c.Round, c.Version, c.CreatedAt, c.UpdatedAt, c.ProcessVersionId, ProcessName = definition.Name, ProcessNumber = c.ProcessVersion.Number,
            Response = response == null ? null : new { Note = response.PublicNote, Actor = external ? "" : response.Actor, response.At, Final = definition.States.Single(s => s.Key == c.State).Terminal },
            Data = data, Fields = fields.Select(f => new { f.Key, f.Label, f.Type, f.Required, Editable = CanEdit(f, c, user) }),
            RequiredDocuments = definition.Documents, CanUpload = CanUpload(c, user), CanWrite = CanWrite(user, c),
            Actions = definition.Transitions.Where(t => CanWrite(user, c) && t.From == c.State && t.Trigger == "user" && t.Roles.Contains(user.Role)).Select(t =>
            {
                var decision = PayloadRules.Decide(definition, t, Json.Read<Dictionary<string, string>>(c.DataJson));
                return new { t.Key, t.Label, NeedsReason = t.Guard == "reason", BlockedReason = t.Guard == "reason" ? null : GuardError(c, t, ""), decision.To, ToLabel = definition.States.Single(s => s.Key == decision.To).Label, RouteKey = external ? null : decision.RouteKey, RouteLabel = external ? null : decision.RouteLabel, DecisionReason = !external && (t.Routes?.Length ?? 0) > 0 ? decision.Reason : null };
            }),
            Documents = c.Documents.OrderByDescending(d => d.Number).Select(d => new { d.Id, d.Kind, d.Number, d.FileName, d.Size, d.ValidUntil, d.UploadedAt, Valid = ValidDocument(d), Current = CurrentDocuments(c).Any(x => x.Id == d.Id) }),
            Tasks = c.Tasks.Where(_ => !external).OrderByDescending(t => t.Round),
            History = c.History.Where(h => !external || h.IsPublic).OrderByDescending(h => h.At).Select(h => new
            {
                h.Id, Actor = external ? "" : h.Actor, h.Action, Note = external ? h.PublicNote ?? "" : h.Note,
                h.Round, h.At, h.IsPublic
            }) };
    }
}
