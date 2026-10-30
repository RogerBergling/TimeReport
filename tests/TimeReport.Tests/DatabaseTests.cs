using Microsoft.Data.Sqlite;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void CreatesSchema()
    {
        using var context = new TestContext();
        Assert.Equal(Database.SchemaVersion, context.Database.SchemaVersionNumber);

        using var command = context.Database.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var tables = new HashSet<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        Assert.Contains("projects", tables);
        Assert.Contains("entries", tables);
        Assert.Contains("entry_types", tables);

        using var foreignKeys = context.Database.Connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, foreignKeys.ExecuteScalar());
    }

    [Fact]
    public void DataPersistsAfterReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "timereport-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "persist.db");

        try
        {
            using (var first = new Database(path))
            {
                first.Open();
                var service = new TimeReportService(first);
                var project = service.CreateProject("Persisted");
                service.SaveEntry(TestData.Entry(
                    TestData.Monday,
                    EntryKind.Work,
                    "08:00",
                    "16:00",
                    project.Id));
            }

            using (var second = new Database(path))
            {
                second.Open();
                var service = new TimeReportService(second);
                Assert.Equal(["Persisted"], service.ListProjects().Select(item => item.Name));
                Assert.Single(service.ListEntries(TestData.Monday, TestData.Monday));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ProjectsAreTrimmedAndUnique()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("  Acme   Corp  ");
        Assert.Equal("Acme Corp", project.Name);
        Assert.Throws<ValidationException>(() => context.Service.CreateProject("acme corp"));
    }

    [Fact]
    public void RenamesArchivesAndDeletesProjects()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Old name");
        var renamed = context.Service.RenameProject(project.Id, "New name");
        Assert.Equal("New name", renamed.Name);
        Assert.Equal("New name", context.Service.GetProject(project.Id)!.Name);

        context.Service.SetProjectArchived(project.Id, true);
        Assert.Empty(context.Service.ListProjects());
        Assert.Single(context.Service.ListProjects(true));
        context.Service.SetProjectArchived(project.Id, false);
        Assert.Single(context.Service.ListProjects());

        context.Service.DeleteProject(project.Id);
        Assert.Null(context.Service.GetProject(project.Id));
    }

    [Fact]
    public void UsedProjectCannotBeDeleted()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));

        Assert.Equal(1, context.Service.ProjectEntryCount(project.Id));
        Assert.Throws<ValidationException>(() => context.Service.DeleteProject(project.Id));
    }

    [Fact]
    public void EntryCrudAndProjectName()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var saved = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id));

        Assert.NotNull(saved.Id);
        Assert.Equal("Acme", saved.ProjectName);
        Assert.Equal(480, saved.DurationMinutes);
        Assert.Equal(saved, context.Service.GetEntry(saved.Id!.Value));

        var updated = context.Service.SaveEntry(saved with
        {
            StartMinute = TimeUtil.ParseTime("09:00"),
            EndMinute = TimeUtil.ParseTime("17:00"),
        });
        Assert.Equal(saved.Id, updated.Id);
        Assert.Equal(540, updated.StartMinute);

        context.Service.RenameProject(project.Id, "Renamed");
        Assert.Equal("Renamed", context.Service.GetEntry(saved.Id!.Value)!.ProjectName);

        context.Service.DeleteEntry(saved.Id!.Value);
        Assert.Null(context.Service.GetEntry(saved.Id!.Value));
    }

    [Fact]
    public void MigratesVersionOneDatabaseAndBackfillsEntryTypes()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "v1.db");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
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
                    INSERT INTO projects (name) VALUES ('Acme');
                    INSERT INTO entries (
                        entry_date, kind, project_id, start_minute, end_minute
                    ) VALUES ('2026-10-26', 'work', 1, 480, 960);
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
            }

            using var database = new Database(path);
            database.Open();
            Assert.Equal(2, database.SchemaVersionNumber);
            var service = new TimeReportService(database);
            Assert.Equal(4, service.ListEntryTypes().Count);

            var entry = Assert.Single(service.ListEntries(
                new DateOnly(2026, 10, 26),
                new DateOnly(2026, 10, 26)));
            Assert.Equal("Work", entry.TypeName);
            Assert.Equal(EntryKind.Work, entry.Kind);
            Assert.NotNull(entry.TypeId);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BackupFileIsValidAndUnlocked()
    {
        using var context = new TestContext();
        var backup = context.Database.BackupTo(Path.Combine(context.Directory, "backup.db"));
        Assert.True(File.Exists(backup));
        Assert.Equal(Database.SchemaVersion, Database.ValidateFile(backup));

        // This specifically catches Windows file-lock regressions.
        File.Delete(backup);
        Assert.False(File.Exists(backup));
    }

    [Fact]
    public void BackupToLivePathIsRejected()
    {
        using var context = new TestContext();
        Assert.Throws<DatabaseException>(() => context.Database.BackupTo(context.Database.Path));
    }

    [Fact]
    public void BackupCanBeOpenedAsDatabase()
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

        using var reopened = new Database(backup);
        reopened.Open();
        Assert.Equal(["Acme"], new TimeReportService(reopened).ListProjects().Select(item => item.Name));
    }
}
