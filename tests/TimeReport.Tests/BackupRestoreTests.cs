using Microsoft.Data.Sqlite;
using TimeReport.Data;
using TimeReport.Models;

namespace TimeReport.Tests;

public sealed class BackupRestoreTests
{
    [Fact]
    public void BackupAndRestoreRoundTrip()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));

        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));
        var second = context.Service.CreateProject("Second");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "16:00",
            "17:00",
            second.Id));
        Assert.Equal(2, context.Service.ListEntries(TestData.Monday, TestData.Monday).Count);

        var autoBackup = context.Database.RestoreFrom(backup);
        Assert.True(File.Exists(autoBackup));
        Assert.Equal(Path.Combine(context.Directory, "backups"), Path.GetDirectoryName(autoBackup));
        Assert.Equal(["Acme"], context.Service.ListProjects().Select(item => item.Name));
        Assert.Single(context.Service.ListEntries(TestData.Monday, TestData.Monday));
    }

    [Fact]
    public void InvalidRestoreFileIsRejectedAndDataRemains()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));

        var invalid = Path.Combine(context.Directory, "invalid.db");
        File.WriteAllText(invalid, "this is not a sqlite database");

        Assert.Throws<RestoreException>(() => context.Database.RestoreFrom(invalid));
        Assert.Single(context.Service.ListEntries(TestData.Monday, TestData.Monday));
    }

    [Fact]
    public void WrongSchemaIsRejected()
    {
        using var context = new TestContext();
        var wrong = Path.Combine(context.Directory, "wrong.db");
        using (var connection = new SqliteConnection($"Data Source={wrong};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE unrelated (id INTEGER); PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<RestoreException>(() => context.Database.RestoreFrom(wrong));
        Assert.Contains("not a TimeReport database", exception.Message);
    }

    [Fact]
    public void NewerSchemaIsRejected()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));

        using (var connection = new SqliteConnection($"Data Source={backup};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 999";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<RestoreException>(() => context.Database.RestoreFrom(backup));
        Assert.Contains("schema version 999", exception.Message);
    }

    [Fact]
    public void NonRegularBackupFileIsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var context = new TestContext();
        var link = Path.Combine(context.Directory, "device.db");
        File.CreateSymbolicLink(link, "/dev/null");

        var exception = Assert.Throws<RestoreException>(() =>
            Database.ValidateFile(link));
        Assert.Contains("regular file", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestoreFromLiveDatabaseIsRejected()
    {
        using var context = new TestContext();
        Assert.Throws<RestoreException>(() => context.Database.RestoreFrom(context.Database.Path));
    }

    [Fact]
    public void RestoreCanRecoverFromSemanticallyDamagedLiveDatabase()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "good.db"));

        using (var command = context.Database.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE entries SET entry_date = 'not-a-date'";
            command.ExecuteNonQuery();
        }

        var autoBackup = context.Database.RestoreFrom(backup);
        Assert.True(File.Exists(autoBackup));
        Assert.Single(context.Service.ListEntries(TestData.Monday, TestData.Monday));
    }

    [Fact]
    public void SemanticInvalidBackupIsRejected()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));

        using (var connection = new SqliteConnection($"Data Source={backup};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE entries SET entry_date = 'not-a-date'";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<RestoreException>(() => Database.ValidateFile(backup));
        Assert.Contains("invalid entry dates", exception.Message);
    }

    [Fact]
    public void BackupWithUnexpectedTriggerIsRejected()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));

        using (var connection = new SqliteConnection($"Data Source={backup};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TRIGGER unexpected_trigger AFTER INSERT ON projects BEGIN SELECT 1; END;";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<RestoreException>(() => Database.ValidateFile(backup));
        Assert.Contains("unexpected trigger", exception.Message);
    }

    [Fact]
    public void BackupWithUnexpectedViewIsRejected()
    {
        using var context = new TestContext();
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));

        using (var connection = new SqliteConnection($"Data Source={backup};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE VIEW unexpected_view AS SELECT 1;";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<RestoreException>(() => Database.ValidateFile(backup));
        Assert.Contains("unexpected view", exception.Message);
    }

    [Fact]
    public void RestoreFromVersionOneBackupMigratesData()
    {
        using var context = new TestContext();
        var v1Path = Path.Combine(context.Directory, "v1.db");
        using (var connection = new SqliteConnection($"Data Source={v1Path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE projects (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    archived INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                );
                CREATE TABLE entries (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    entry_date TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    project_id INTEGER REFERENCES projects(id) ON DELETE RESTRICT,
                    label TEXT,
                    start_minute INTEGER NOT NULL,
                    end_minute INTEGER NOT NULL,
                    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
                    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                );
                INSERT INTO projects (name) VALUES ('Legacy');
                INSERT INTO entries (entry_date, kind, project_id, start_minute, end_minute)
                VALUES ('2026-10-26', 'work', 1, 480, 960);
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }

        context.Database.RestoreFrom(v1Path);

        Assert.Equal(Database.SchemaVersion, context.Database.SchemaVersionNumber);
        var entry = Assert.Single(context.Service.ListEntries(TestData.Monday, TestData.Monday));
        Assert.Equal("Legacy", entry.ProjectName);
        Assert.NotNull(entry.TypeId);
        Assert.Equal("Work", entry.TypeName);
    }

    [Fact]
    public void RestoreCreatesPreRestoreBackup()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));

        var autoBackup = context.Database.RestoreFrom(backup);
        Assert.True(File.Exists(autoBackup));
        Assert.StartsWith("pre-restore-", Path.GetFileName(autoBackup));
    }
}
