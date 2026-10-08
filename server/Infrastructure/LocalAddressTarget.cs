using Microsoft.Data.Sqlite;

namespace Workflow.Infrastructure;

// Local demo only. Separate from inquiry persistence; no real integration or credentials.
public sealed class LocalAddressTarget(IWebHostEnvironment environment)
{
    private string Connection => "Data Source=" + Path.Combine(environment.ContentRootPath, "App_Data", "fictional-addresses.db");
    public async Task Initialize()
    {
        await using var db = new SqliteConnection(Connection); await db.OpenAsync();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Addresses (ProviderId INTEGER PRIMARY KEY, Address TEXT NOT NULL, Mode TEXT NOT NULL DEFAULT 'normal', Changes INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS Receipts (OperationId TEXT PRIMARY KEY, Request TEXT NOT NULL, Result TEXT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> Read(int providerId)
    {
        await using var db = new SqliteConnection(Connection); await db.OpenAsync();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Address, Changes, Mode FROM Addresses WHERE ProviderId=$id";
        command.Parameters.AddWithValue("$id", providerId);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new { providerId, address = reader.GetString(0), changes = reader.GetInt32(1), mode = reader.GetString(2), fictional = true } : null;
    }

    // Explicit, local administrator setup. Never changes platform Provider records.
    public async Task Configure(int providerId, string address, string mode)
    {
        Engine.Require(!string.IsNullOrWhiteSpace(address) && address.Length <= 250 && new[] { "normal", "reject", "interruptOnce", "slow" }.Contains(mode), "הגדרת יעד הדגמה אינה תקינה");
        await using var db = new SqliteConnection(Connection); await db.OpenAsync();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO Addresses(ProviderId,Address,Mode) VALUES($id,$address,$mode) ON CONFLICT(ProviderId) DO UPDATE SET Address=$address, Mode=$mode";
        command.Parameters.AddWithValue("$id", providerId); command.Parameters.AddWithValue("$address", address); command.Parameters.AddWithValue("$mode", mode);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<BusinessResult?> Recover(BusinessRequest request, CancellationToken cancellation)
    {
        await using var db = new SqliteConnection(Connection); await db.OpenAsync(cancellation);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Request, Result FROM Receipts WHERE OperationId=$operation";
        command.Parameters.AddWithValue("$operation", request.OperationId);
        using var reader = await command.ExecuteReaderAsync(cancellation);
        if (!await reader.ReadAsync(cancellation)) return null;
        Engine.Require(SameRequest(Json.Read<BusinessRequest>(reader.GetString(0)), request), "מפתח הפעולה כבר משויך לקלט אחר", 409);
        return Json.Read<BusinessResult>(reader.GetString(1));
    }

    public Task<BusinessResult> Change(BusinessRequest request, CancellationToken cancellation) => ChangeCore(request, cancellation, true);
    // A durable target job has already waited until its due date; do not wait a second time.
    public Task<BusinessResult> ExecuteReady(BusinessRequest request, CancellationToken cancellation) => ChangeCore(request, cancellation, false);
    public static bool SameRequest(BusinessRequest a, BusinessRequest b) => a.OperationId == b.OperationId && a.ProviderId == b.ProviderId &&
        a.Inputs.Count == b.Inputs.Count && a.Inputs.All(p => b.Inputs.TryGetValue(p.Key, out var value) && value == p.Value);

    private async Task<BusinessResult> ChangeCore(BusinessRequest request, CancellationToken cancellation, bool simulateLatency)
    {
        var address = request.Inputs["address"].Trim();
        await using var db = new SqliteConnection(Connection); await db.OpenAsync(cancellation);
        // Controlled latency occurs outside the transaction, never holding SQLite's write lock.
        if (simulateLatency) using (var modeCommand = db.CreateCommand())
        {
            modeCommand.CommandText = "SELECT Mode FROM Addresses WHERE ProviderId=$id";
            modeCommand.Parameters.AddWithValue("$id", request.ProviderId);
            if (await modeCommand.ExecuteScalarAsync(cancellation) is "slow")
                await Task.Delay(TimeSpan.FromSeconds(10), cancellation);
        }
        // SQLite immediate transaction serializes concurrent deliveries and the unique receipt.
        using var transaction = db.BeginTransaction();
        using var command = db.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT Request, Result FROM Receipts WHERE OperationId=$operation";
        command.Parameters.AddWithValue("$operation", request.OperationId);
        using (var reader = await command.ExecuteReaderAsync(cancellation))
        {
            if (await reader.ReadAsync(cancellation))
            {
                Engine.Require(SameRequest(Json.Read<BusinessRequest>(reader.GetString(0)), request), "מפתח הפעולה כבר משויך לקלט אחר", 409);
                return Json.Read<BusinessResult>(reader.GetString(1));
            }
        }
        command.CommandText = "SELECT Address, Mode FROM Addresses WHERE ProviderId=$id";
        command.Parameters.AddWithValue("$id", request.ProviderId);
        string? before = null; var mode = "normal";
        using (var reader = await command.ExecuteReaderAsync(cancellation))
            if (await reader.ReadAsync(cancellation)) { before = reader.GetString(0); mode = reader.GetString(1); }
        var success = before != null && mode != "reject" && address.Length is > 0 and <= 250;
        var result = success
            ? new BusinessResult(true, $"הכתובת עודכנה מ־{before} ל־{address}.", new() { ["before"] = before!, ["after"] = address })
            : new BusinessResult(false, "עדכון הכתובת נדחה ביעד ההדגמה; הכתובת לא השתנתה.", new() { ["before"] = before ?? "", ["after"] = before ?? "" });
        if (success)
        {
            command.CommandText = "UPDATE Addresses SET Address=$address, Changes=Changes+1 WHERE ProviderId=$id";
            command.Parameters.AddWithValue("$address", address);
            await command.ExecuteNonQueryAsync(cancellation);
        }
        command.CommandText = "INSERT INTO Receipts(OperationId,Request,Result) VALUES($operation,$request,$result)";
        command.Parameters.AddWithValue("$request", Json.Write(request)); command.Parameters.AddWithValue("$result", Json.Write(result));
        await command.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        // Controlled demo fault proves retry after target commit but before platform acknowledgement.
        if (success && mode == "interruptOnce") throw new IOException("Demo interruption after target commit");
        return result;
    }
}

public record LocalAddressSetup(string Address, string Mode = "normal");
