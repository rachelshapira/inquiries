using Microsoft.EntityFrameworkCore;

namespace Workflow;

// Local SQLite demo only. Production SQL Server upgrades use EF Core migrations.
public static class DemoUpgrade
{
    public static async Task Apply(WorkflowDb db)
    {
        Engine.Require(db.Database.IsSqlite(), "שדרוג ההדגמה מיועד ל־SQLite בלבד");
        var sql = db.Database.GenerateCreateScript()
            .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
            .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ")
            .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ");
        await db.Database.ExecuteSqlRawAsync(sql);
        // Preserve demo data created before the mail outbox columns were introduced.
        await db.Database.OpenConnectionAsync();
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('Outbox')";
            var columns = new HashSet<string>();
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            if (!columns.Contains("Kind")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Outbox ADD COLUMN Kind TEXT NOT NULL DEFAULT 'notification'");
            using var accountColumns = db.Database.GetDbConnection().CreateCommand();
            accountColumns.CommandText = "PRAGMA table_info('Accounts')";
            var names = new HashSet<string>();
            await using (var reader = await accountColumns.ExecuteReaderAsync()) while (await reader.ReadAsync()) names.Add(reader.GetString(1));
            if (!names.Contains("ReadAccess")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Accounts ADD COLUMN ReadAccess TEXT NOT NULL DEFAULT 'subtree'");
            if (!names.Contains("WriteAccess")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Accounts ADD COLUMN WriteAccess TEXT NOT NULL DEFAULT 'subtree'");
            if (!names.Contains("Version")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Accounts ADD COLUMN Version INTEGER NOT NULL DEFAULT 1");
            using var ruleColumns = db.Database.GetDbConnection().CreateCommand();
            ruleColumns.CommandText = "PRAGMA table_info('RoutingRules')";
            var ruleNames = new HashSet<string>();
            await using (var reader = await ruleColumns.ExecuteReaderAsync()) while (await reader.ReadAsync()) ruleNames.Add(reader.GetString(1));
            if (!ruleNames.Contains("ProcessKey")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE RoutingRules ADD COLUMN ProcessKey TEXT NULL");
            if (!columns.Contains("Recipient")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Outbox ADD COLUMN Recipient TEXT NOT NULL DEFAULT ''");
            if (!columns.Contains("ClaimId")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Outbox ADD COLUMN ClaimId TEXT NULL");
            if (!columns.Contains("ClaimedAt")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Outbox ADD COLUMN ClaimedAt TEXT NULL");
            if (!columns.Contains("LeaseUntil")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Outbox ADD COLUMN LeaseUntil TEXT NULL");
            using var historyColumns = db.Database.GetDbConnection().CreateCommand();
            historyColumns.CommandText = "PRAGMA table_info('History')";
            var historyNames = new HashSet<string>();
            await using (var reader = await historyColumns.ExecuteReaderAsync()) while (await reader.ReadAsync()) historyNames.Add(reader.GetString(1));
            if (!historyNames.Contains("IsPublic")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE History ADD COLUMN IsPublic INTEGER NOT NULL DEFAULT 0");
            if (!historyNames.Contains("PublicNote")) await db.Database.ExecuteSqlRawAsync("ALTER TABLE History ADD COLUMN PublicNote TEXT NULL");
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
