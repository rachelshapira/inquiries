using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Workflow;
using Workflow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var demo = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo");
var connection = builder.Configuration.GetConnectionString("Workflow");
var storage = Path.GetFullPath(builder.Configuration["StoragePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "files"));
Directory.CreateDirectory(storage);
builder.Services.AddDbContext<WorkflowDb>(options =>
{
    if (demo) options.UseSqlite("Data Source=" + Path.Combine(builder.Environment.ContentRootPath, "App_Data", "demo.db"));
    else options.UseSqlServer(connection ?? throw new InvalidOperationException("ConnectionStrings:Workflow is required outside local demo mode."));
});
if (demo)
    builder.Services.AddAuthentication("demo").AddCookie("demo", options =>
    {
        options.Cookie.Name = "workflow.demo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    });
else
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Authority"]) || string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Audience"]))
        throw new InvalidOperationException("Organizational JWT authority and audience must be configured.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
    });
}
builder.Services.AddAuthorization();
var callbacks = builder.Configuration.GetSection("BusinessCallbacks").Get<BusinessCallbackOptions>() ?? new();
BusinessResultsApi.Register(builder.Services, callbacks, demo);
if (demo) builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
builder.Services.AddSingleton<Workflow.Infrastructure.IMailSender, Workflow.Infrastructure.NullMailSender>();
if (demo)
{
    builder.Services.AddSingleton<LocalAddressTarget>();
    builder.Services.AddSingleton<BusinessHandler>(services =>
    {
        var target = services.GetRequiredService<LocalAddressTarget>();
        return new("changeAddress", "עדכון כתובת ביעד הדגמה מקומי", ["address"], target.Change, target.Recover);
    });
}
builder.Services.AddSingleton<BusinessActions>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Outbox").Get<OutboxOptions>() ?? new());
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 11 * 1024 * 1024);
var app = builder.Build();

if (args.Contains("--schema"))
{
    using var scope = app.Services.CreateScope();
    Console.WriteLine(scope.ServiceProvider.GetRequiredService<WorkflowDb>().Database.GenerateCreateScript());
    return;
}
if (demo)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkflowDb>();
    await db.Database.EnsureCreatedAsync();
    await DemoUpgrade.Apply(db);
    await Seed.Initialize(db);
    await app.Services.GetRequiredService<LocalAddressTarget>().Initialize();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try
    {
        if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            Engine.Require(context.Request.Headers["X-Workflow-Client"] == "portal", "כותרת אבטחת הבקשה חסרה", 403);
        await next();
    }
    catch (RuleException ex) { context.Response.StatusCode = ex.Status; await context.Response.WriteAsJsonAsync(new { error = ex.Message }); }
    catch (DbUpdateConcurrencyException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = "הרשומה עודכנה במקביל. יש לרענן ולנסות שוב" }); }
    catch (DbUpdateException ex) { app.Logger.LogWarning(ex, "Database constraint rejected operation"); context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = "לא ניתן לשמור: רשומה כפולה או עדכון מתנגש" }); }
    catch (BadHttpRequestException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "הבקשה אינה תקינה או הקובץ גדול מדי" }); }
    catch (System.Text.Json.JsonException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "מבנה הנתונים אינו תקין" }); }
    catch (Exception ex) { app.Logger.LogError(ex, "Unhandled request failure"); context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = "הפעולה נכשלה. הנתונים לא נשמרו; יש לנסות שוב" }); }
});
app.UseAuthentication();
app.UseAuthorization();
if (callbacks.Enabled) BusinessResultsApi.Map(app, demo);
app.MapGet("/api/session", async (HttpContext context, WorkflowDb db) =>
{
    var id = context.User.FindFirstValue("sub");
    var user = id == null ? null : await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.Active);
    return Results.Ok(new { demo, user, accounts = demo ? await db.Accounts.Where(a => a.Active).ToListAsync() : [] });
});
if (demo)
{
    app.MapPost("/api/demo/login/{id}", async (string id, HttpContext context, WorkflowDb db) =>
    {
        Engine.Require(context.Connection.RemoteIpAddress == null || System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress), "הדגמה זמינה רק מהמחשב המקומי", 403);
        var user = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.Active);
        Engine.Require(user != null, "משתמש לא נמצא", 404);
        await context.SignInAsync("demo", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id)], "demo")));
        return Results.Ok(user);
    });
    app.MapPost("/api/logout", async (HttpContext context) => { await context.SignOutAsync("demo"); return Results.NoContent(); });
}
var api = app.MapGroup("/api").RequireAuthorization();
api.AddEndpointFilter(async (invocation, next) =>
{
    var context = invocation.HttpContext;
    var db = context.RequestServices.GetRequiredService<WorkflowDb>();
    var id = context.User.FindFirstValue("sub");
    var user = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.Active);
    Engine.Require(user != null && Engine.Roles.Contains(user.Role), "המשתמש אינו מורשה או אינו פעיל", 403);
    user!.Scope = await OrgTree.Scope(db, user.Unit, user.ReadAccess);
    user.WriteScope = await OrgTree.Scope(db, user.Unit, user.WriteAccess);
    if (context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        Engine.Require(user.WriteAccess != "none", "למשתמש הרשאת קריאה בלבד", 403);
    context.Items["account"] = user!;
    return await next(invocation);
});
static Account User(HttpContext context) => (Account)context.Items["account"]!;
RoutingApi.Map(api);
OrganizationApi.Map(api);

api.MapGet("/providers", async (WorkflowDb db, HttpContext context) =>
{
    var u = User(context);
    return await db.Providers.Where(p => u.ReadAccess == "none" ? false : u.Role == "Provider" ? p.Id == u.ProviderId : u.Scope.Headquarters || u.Scope.Units.Contains(p.Unit)).OrderBy(p => p.Name).ToListAsync();
});
api.MapPost("/providers", async (ProviderInput input, WorkflowDb db, HttpContext context) =>
{
    Engine.Require(User(context).Role == "Admin", "נדרשת הרשאת מנהל", 403);
    var provider = new Provider();
    SetProvider(provider, input);
    Engine.Require(User(context).WriteScope.Contains(input.Unit) && await db.OrgUnits.AnyAsync(u => u.Key == input.Unit), "אין הרשאה ליחידה שנבחרה", 403);
    db.Providers.Add(provider);
    await db.SaveChangesAsync();
    return Results.Ok(provider);
});
api.MapPut("/providers/{id:int}", async (int id, ProviderInput input, WorkflowDb db, HttpContext context) =>
{
    Engine.Require(User(context).Role == "Admin", "נדרשת הרשאת מנהל", 403);
    var p = await db.Providers.FindAsync(id) ?? throw new RuleException("נותן השירות לא נמצא", 404);
    Engine.Require(User(context).WriteScope.Contains(p.Unit) && User(context).WriteScope.Contains(input.Unit) && await db.OrgUnits.AnyAsync(u => u.Key == input.Unit), "אין הרשאה ליחידה זו", 403);
    Engine.Require(p.Version == input.Version, "הפרטים עודכנו במקביל. יש לרענן", 409);
    SetProvider(p, input); p.Version++;
    await db.SaveChangesAsync(); return Results.Ok(p);
});
api.MapGet("/processes", async (WorkflowDb db) => (await db.Processes.OrderByDescending(p => p.Number).ToListAsync()).Select(p => new { p.Id, p.Key, p.Number, p.PublishedAt, Definition = Json.Read<Definition>(p.DefinitionJson) }));
api.MapGet("/catalog", (BusinessActions actions) => new { roles = Engine.Roles, guards = Engine.Guards, effects = Engine.Effects, businessActions = actions.Catalog });
api.MapPost("/processes/simulate", (Simulation input, HttpContext context) =>
{
    Engine.Require(User(context).Role == "Admin", "נדרשת הרשאת מנהל", 403);
    Engine.ValidateDefinition(input.Definition);
    var payload = input.Data ?? [];
    PayloadRules.ValidatePayload(input.Definition, payload);
    var state = input.Definition.InitialState;
    var trace = new List<string> { state };
    var decisions = new List<object>();
    foreach (var action in input.Actions)
    {
        var t = input.Definition.Transitions.SingleOrDefault(t => t.From == state && t.Key == action);
        Engine.Require(t != null, "אין מעבר מתאים מ־" + state + " באמצעות " + action);
        var decision = PayloadRules.Decide(input.Definition, t!, payload);
        decisions.Add(new { Action = t!.Label, From = state, decision.To, decision.RouteKey, decision.RouteLabel, decision.Priority, decision.Reason });
        state = decision.To; trace.Add(state);
    }
    return new { valid = true, trace, decisions, note = "בדיקת מסלול ותנאי payload. הרשאות, מסמכים ותנאי ביצוע נבדקים על הפנייה בזמן הפעולה." };
});
api.MapPost("/processes/preview-action", (PayloadPreview input, HttpContext context) =>
{
    Engine.Require(User(context).Role == "Admin", "נדרשת הרשאת מנהל", 403);
    Engine.ValidateDefinition(input.Definition);
    var payload = input.Data ?? [];
    PayloadRules.ValidatePayload(input.Definition, payload);
    var transition = input.Definition.Transitions.SingleOrDefault(t => t.From == input.State && t.Key == input.Action);
    Engine.Require(transition != null, "הפעולה אינה קיימת בשלב הנבחר");
    var decision = PayloadRules.Decide(input.Definition, transition!, payload);
    return new { decision.To, ToLabel = input.Definition.States.Single(s => s.Key == decision.To).Label, decision.RouteKey, decision.RouteLabel, decision.Priority, decision.Reason };
});
api.MapPost("/processes", async (Publish input, WorkflowDb db, HttpContext context, BusinessActions actions) =>
{
    Engine.Require(User(context).Role == "Admin", "נדרשת הרשאת מנהל", 403);
    Engine.Require(System.Text.RegularExpressions.Regex.IsMatch(input.Key ?? "", "^[a-z][a-z0-9-]{0,59}$"), "מזהה התהליך אינו תקין");
    Engine.ValidateDefinition(input.Definition);
    actions.Validate(input.Definition);
    var latest = await db.Processes.Where(p => p.Key == input.Key).MaxAsync(p => (int?)p.Number) ?? 0;
    Engine.Require(latest == input.BaseNumber, "פורסמה גרסה אחרת במקביל. יש לטעון מחדש", 409);
    var version = new ProcessVersion { Key = input.Key!, Number = latest + 1, DefinitionJson = Json.Write(input.Definition) };
    db.Processes.Add(version); await db.SaveChangesAsync(); return Results.Ok(new { version.Id, version.Number });
});
api.MapGet("/cases", async (WorkflowDb db, HttpContext context) =>
{
    var user = User(context);
    var cases = await Engine.Visible(db, user).Include(c => c.Provider).Include(c => c.ProcessVersion).OrderByDescending(c => c.UpdatedAt).Take(500).ToListAsync();
    return cases.Select(c => new { c.Id, c.Title, c.ProviderId, ProviderName = c.Provider.Name, c.State, StateLabel = Engine.Definition(c).States.Single(s => s.Key == c.State).Label, ProcessName = Engine.Definition(c).Name, c.Round, c.Unit, c.AssigneeId, c.UpdatedAt });
});
api.MapGet("/cases/{id:int}", async (int id, WorkflowDb db, HttpContext context) => Engine.View(await Engine.Load(db, id, User(context)), User(context)));
api.MapGet("/cases/creation-options", async (WorkflowDb db, HttpContext context) =>
{
    var user = User(context);
    var providers = await db.Providers.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
    var versions = await db.Processes.AsNoTracking().OrderByDescending(p => p.Number).ToListAsync();
    var eligibility = versions.GroupBy(p => p.Key).Select(g => g.First()).Select(p => new
    {
        Process = p, ProviderIds = providers.Where(provider => Engine.CanInitiateProcess(user, p, provider)).Select(provider => provider.Id).ToArray()
    }).Where(p => p.ProviderIds.Length > 0).ToArray();
    return Results.Ok(new
    {
        Processes = eligibility.Select(e => new { e.Process.Id, e.Process.Key, e.Process.Number, e.Process.PublishedAt, Definition = Json.Read<Definition>(e.Process.DefinitionJson) }),
        Providers = providers.Where(p => eligibility.Any(e => e.ProviderIds.Contains(p.Id))),
        Eligibility = eligibility.Select(e => new { ProcessVersionId = e.Process.Id, e.ProviderIds })
    });
});
api.MapPost("/cases", async (NewCase input, WorkflowDb db, HttpContext context) =>
{
    var user = User(context);
    var provider = await db.Providers.FindAsync(input.ProviderId) ?? throw new RuleException("נותן השירות לא נמצא", 404);
    Engine.Require(user.Role == "Provider" ? user.ProviderId == provider.Id : user.WriteScope.Contains(provider.Unit), "אין הרשאה לנותן השירות", 403);
    var process = await db.Processes.FindAsync(input.ProcessVersionId) ?? throw new RuleException("התהליך לא נמצא");
    Engine.Require(Engine.CanInitiateProcess(user, process, provider), "אין הרשאה לפתיחת פנייה בתהליך זה", 403);
    Engine.Require(!await db.Processes.AnyAsync(p => p.Key == process.Key && p.Number > process.Number), "יש לבחור את גרסת התהליך האחרונה");
    Engine.Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 160, "נדרשת כותרת עד 160 תווים");
    var item = new Case { Title = input.Title.Trim(), Provider = provider, ProviderId = provider.Id, ProcessVersion = process, ProcessVersionId = process.Id, State = Json.Read<Definition>(process.DefinitionJson).InitialState, Unit = provider.Unit };
    Engine.UpdateFields(item, user, input.Data ?? []);
    var route = await RoutingEngine.Decide(db, provider, process.Key, item.Title, Json.Read<Dictionary<string, string>>(item.DataJson));
    RoutingEngine.Apply(item, route);
    Engine.Touch(item, user, "פתיחת פנייה", "ניתוב אוטומטי ל־" + route.UnitName + " · " + route.RuleName, isPublic: true);
    db.Cases.Add(item); await db.SaveChangesAsync();
    // Routing can legitimately move a branch-created inquiry outside the creator's scope.
    return Engine.CanAccess(user, item) ? Results.Ok(Engine.View(item, user)) : Results.Ok(new { item.Id, item.Unit, unitName = route.UnitName, canView = false, message = "הפנייה נוצרה ונותבה ליחידה המטפלת. אין לך הרשאת צפייה ביחידה זו." });
});
api.MapPut("/cases/{id:int}/data", async (int id, ChangeFields input, WorkflowDb db, HttpContext context) =>
{
    var user = User(context); var item = await Engine.Load(db, id, user); Engine.WriteAccess(user, item); Engine.Version(item, input.Version);
    Engine.UpdateFields(item, user, input.Data); Engine.Touch(item, user, "עדכון פרטים", isPublic: user.Role == "Provider");
    await db.SaveChangesAsync(); return Results.Ok(Engine.View(item, user));
});
api.MapPost("/cases/{id:int}/actions/{action}", async (int id, string action, ActionInput input, WorkflowDb db, HttpContext context, BusinessActions actions) =>
{
    var user = User(context); var item = await Engine.Load(db, id, user); Engine.WriteAccess(user, item); Engine.Version(item, input.Version);
    var transition = Engine.Execute(item, user, action, input.Note ?? "", input.ShareNote);
    actions.Enqueue(db, item, user, transition);
    var mail = transition.Effects.Contains("sendMail");
    db.Outbox.Add(new Outbox
    {
        CaseId = item.Id,
        Kind = mail ? "mail" : "notification",
        Recipient = mail ? item.Provider.Email : "",
        Message = item.Title + ": " + item.History.Last().Action
    });
    // A single SaveChanges transaction commits state, tasks, audit and outbox together.
    await db.SaveChangesAsync(); return Results.Ok(Engine.View(item, user));
});
api.MapGet("/cases/{id:int}/business-actions", async (int id, WorkflowDb db, HttpContext context) =>
{
    await Engine.Load(db, id, User(context));
    return (await db.Outbox.Where(o => o.CaseId == id && o.Kind == "businessAction").OrderBy(o => o.Id).ToListAsync())
        .Select(o => new { o.Id, o.Status, Attempts = User(context).Role == "Provider" ? (int?)null : o.Attempts, Json.Read<BusinessJob>(o.Message).Result });
});
if (demo)
{
    api.MapGet("/demo/business-target/{providerId:int}", async (int providerId, WorkflowDb db, HttpContext context, LocalAddressTarget target) =>
    {
        var provider = await db.Providers.FindAsync(providerId) ?? throw new RuleException("נותן השירות לא נמצא", 404);
        Engine.Access(User(context), new Case { ProviderId = providerId, Unit = provider.Unit });
        return Results.Ok(await target.Read(providerId));
    });
    api.MapPost("/demo/business-target/{providerId:int}", async (int providerId, LocalAddressSetup input, WorkflowDb db, HttpContext context, LocalAddressTarget target) =>
    {
        var user = User(context); OrgTree.Manage(user);
        Engine.Require(context.Connection.RemoteIpAddress == null || System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress), "יעד הדגמה זמין מקומית בלבד", 403);
        Engine.Require(await db.Providers.AnyAsync(p => p.Id == providerId), "נותן השירות לא נמצא", 404);
        await target.Configure(providerId, input.Address, input.Mode);
        return Results.Ok(await target.Read(providerId));
    });
}
api.MapPost("/cases/{id:int}/assign", async (int id, Assign input, WorkflowDb db, HttpContext context) =>
{
    var user = User(context); Engine.Require(user.Role is "Admin" or "Reviewer", "אין הרשאת ניתוב", 403);
    var item = await Engine.Load(db, id, user); Engine.WriteAccess(user, item); Engine.Version(item, input.Version);
    var assignee = await db.Accounts.SingleOrDefaultAsync(a => a.Id == input.AssigneeId && a.Active && a.Role == "Reviewer");
    Engine.Require(assignee != null && user.Scope.Contains(assignee.Unit) && (await OrgTree.Scope(db, assignee.Unit, assignee.WriteAccess)).Contains(item.Unit), "יש לבחור מטפל פעיל בעל הרשאה ליחידה המטפלת");
    item.AssigneeId = assignee!.Id; Engine.Touch(item, user, "שיוך מטפל", assignee.Name);
    await db.SaveChangesAsync(); return Results.Ok(Engine.View(item, user));
});
api.MapGet("/reviewers", async (WorkflowDb db, HttpContext context) =>
{
    var user = User(context); Engine.Require(user.Role != "Provider", "אין הרשאה", 403);
    return await db.Accounts.Where(a => a.Active && a.Role == "Reviewer" && (user.Scope.Headquarters || user.Scope.Units.Contains(a.Unit))).Select(a => new { a.Id, a.Name, a.Unit }).ToListAsync();
});
api.MapGet("/cases/{id:int}/reviewers", async (int id, WorkflowDb db, HttpContext context) =>
{
    var user = User(context); Engine.Require(user.Role is "Admin" or "Reviewer", "אין הרשאת ניתוב", 403);
    var item = await Engine.Load(db, id, user);
    var tree = await db.OrgUnits.AsNoTracking().ToListAsync();
    var reviewers = await db.Accounts.Where(a => a.Active && a.Role == "Reviewer").ToListAsync();
    return reviewers.Where(a => user.Scope.Contains(a.Unit) && OrgTree.EffectiveScope(tree, a.Unit, a.WriteAccess).Contains(item.Unit)).Select(a => new { a.Id, a.Name, a.Unit });
});
api.MapGet("/tasks", async (WorkflowDb db, HttpContext context) =>
{
    var user = User(context); Engine.Require(user.Role != "Provider", "משימות הבדיקה מיועדות לעובדים בלבד", 403);
    var ids = Engine.Visible(db, user).Select(c => c.Id);
    return await db.Tasks.Where(t => ids.Contains(t.CaseId)).OrderByDescending(t => t.Id).Take(500).ToListAsync();
});
api.MapPost("/cases/{id:int}/tasks/{taskId:int}", async (int id, int taskId, Review input, WorkflowDb db, HttpContext context) =>
{
    var user = User(context); Engine.Require(user.Role is "Reviewer" or "Admin", "נדרשת הרשאת בודק", 403);
    var item = await Engine.Load(db, id, user); Engine.WriteAccess(user, item); Engine.Version(item, input.Version);
    Engine.Require(item.AssigneeId == null || item.AssigneeId == user.Id || user.Role == "Admin", "הפנייה משויכת למטפל אחר", 403);
    var task = item.Tasks.SingleOrDefault(t => t.Id == taskId);
    Engine.Require(task != null && task.Round == item.Round && task.Status == "open", "המשימה אינה פתוחה בסבב הנוכחי");
    Engine.Require(Engine.Definition(item).Transitions.Any(t => t.From == item.State && t.Guard == "reviewsPassed" && t.Roles.Any(r => r is "Reviewer" or "Admin")), "הפנייה אינה בשלב בדיקה");
    Engine.Require(input.Result is "passed" or "failed", "תוצאת בדיקה לא תקינה");
    Engine.Require(input.Note.Length <= 4000 && (input.Result != "failed" || !string.IsNullOrWhiteSpace(input.Note)), "נדרש פירוט לדחיית מסמך");
    task!.Status = "completed"; task.Result = input.Result; task.Note = input.Note; task.ReviewerId = user.Id;
    Engine.Touch(item, user, input.Result == "passed" ? "מסמך עבר בדיקה" : "מסמך לא עבר בדיקה", input.Note);
    await db.SaveChangesAsync(); return Results.Ok(Engine.View(item, user));
});
api.MapPost("/cases/{id:int}/documents", async (int id, HttpContext context, WorkflowDb db) =>
{
    var user = User(context); var item = await Engine.Load(db, id, user); Engine.WriteAccess(user, item);
    Engine.Require(Engine.CanUpload(item, user), "לא ניתן להעלות מסמך בשלב זה", 403);
    var form = await context.Request.ReadFormAsync();
    Engine.Require(long.TryParse(form["version"], out var version), "חסרה גרסת רשומה"); Engine.Version(item, version);
    var kind = form["kind"].ToString(); Engine.Require(Engine.Definition(item).Documents.Contains(kind), "סוג המסמך אינו נדרש בתהליך");
    var file = form.Files.GetFile("file"); Engine.Require(file is { Length: > 0 and <= 10 * 1024 * 1024 }, "יש לבחור קובץ PDF או תמונה עד 10MB");
    var ext = Path.GetExtension(file!.FileName).ToLowerInvariant(); Engine.Require(new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(ext), "סוג קובץ לא נתמך");
    DateTime? validUntil = null;
    if (!string.IsNullOrEmpty(form["validUntil"]))
    {
        Engine.Require(DateOnly.TryParseExact(form["validUntil"], "yyyy-MM-dd", out var date), "תאריך תוקף אינו תקין");
        validUntil = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
    var key = Guid.NewGuid().ToString("N"); var path = Path.Combine(storage, key);
    try
    {
        await using (var stream = File.Create(path)) await file.CopyToAsync(stream);
        var header = new byte[8]; await using (var stream = File.OpenRead(path)) await stream.ReadExactlyAsync(header);
        var valid = ext == ".pdf" ? header.AsSpan(0, 5).SequenceEqual("%PDF-"u8) : ext == ".png" ? header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : header[0] == 255 && header[1] == 216 && header[2] == 255;
        Engine.Require(valid, "תוכן הקובץ אינו תואם לסוג שנבחר");
        item.Documents.Add(new Document { Kind = kind, Number = item.Documents.Where(d => d.Kind == kind).Select(d => d.Number).DefaultIfEmpty(0).Max() + 1, FileName = Path.GetFileName(file.FileName), StorageKey = key, Size = file.Length, ValidUntil = validUntil });
        Engine.Touch(item, user, "העלאת מסמך", kind, isPublic: true, publicNote: kind); await db.SaveChangesAsync();
    }
    catch { if (File.Exists(path)) File.Delete(path); throw; }
    return Results.Ok(Engine.View(item, user));
});
api.MapGet("/cases/{id:int}/documents/{documentId:int}", async (int id, int documentId, WorkflowDb db, HttpContext context) =>
{
    var item = await Engine.Load(db, id, User(context));
    var doc = item.Documents.SingleOrDefault(d => d.Id == documentId) ?? throw new RuleException("המסמך לא נמצא", 404);
    var path = Path.Combine(storage, doc.StorageKey); Engine.Require(File.Exists(path), "קובץ המסמך אינו זמין באחסון", 404);
    return Results.File(path, "application/octet-stream", doc.FileName);
});
api.MapGet("/notifications", async (WorkflowDb db, HttpContext context) =>
{
    var ids = Engine.Visible(db, User(context)).Select(c => c.Id);
    return await db.Notifications.Where(n => ids.Contains(n.CaseId)).OrderByDescending(n => n.Id).Take(50).ToListAsync();
});
api.MapGet("/reports", async (WorkflowDb db, HttpContext context) =>
{
    var user = User(context);
    var ids = Engine.Visible(db, user).Select(c => c.Id);
    return new { total = await ids.CountAsync(), states = await Engine.Visible(db, User(context)).GroupBy(c => c.State).Select(g => new { state = g.Key, count = g.Count() }).ToListAsync(), openTasks = user.Role == "Provider" ? 0 : await db.Tasks.CountAsync(t => ids.Contains(t.CaseId) && t.Status == "open"), overdueTasks = user.Role == "Provider" ? 0 : await db.Tasks.CountAsync(t => ids.Contains(t.CaseId) && t.Status == "open" && t.DueAt < DateTime.UtcNow) };
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
await app.RunAsync();

static void SetProvider(Provider p, ProviderInput input)
{
    Engine.Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 160 && System.Text.RegularExpressions.Regex.IsMatch(input.Registration ?? "", "^[0-9]{5,12}$"), "נדרשים שם ומספר רישום תקין");
    Engine.Require(!string.IsNullOrWhiteSpace(input.Unit) && input.Unit.Length <= 80, "נדרשת יחידה");
    Engine.Require(input.Email.Length <= 200 && (input.Email == "" || System.Net.Mail.MailAddress.TryCreate(input.Email, out _)), "כתובת דואר לא תקינה");
    Engine.Require(new[] { input.Branches, input.ContactName, input.Phone, input.Agreement }.All(v => v != null && v.Length <= 4000), "פרטי נותן השירות ארוכים מדי");
    p.Name = input.Name.Trim(); p.Registration = input.Registration!; p.Unit = input.Unit; p.Branches = input.Branches; p.ContactName = input.ContactName; p.Email = input.Email; p.Phone = input.Phone; p.Agreement = input.Agreement;
}
public record NewCase(int ProviderId, int ProcessVersionId, string Title, Dictionary<string, string>? Data = null);
public record ChangeFields(long Version, Dictionary<string, string> Data);
public record ActionInput(long Version, string? Note, bool ShareNote = false);
public record Assign(long Version, string AssigneeId);
public record Review(long Version, string Result, string Note);
public record Publish(string Key, int BaseNumber, Definition Definition);
public record Simulation(Definition Definition, string[] Actions, Dictionary<string, string>? Data = null);
public record PayloadPreview(Definition Definition, string State, string Action, Dictionary<string, string>? Data = null);
public record ProviderInput(long Version, string Name, string Registration, string Unit, string Branches, string ContactName, string Email, string Phone, string Agreement);
