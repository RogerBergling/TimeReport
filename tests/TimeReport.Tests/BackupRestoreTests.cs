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
    public void RestoreFromLiveDatabaseIsRejected()
    {
        using var context = new TestContext();
        Assert.Throws<RestoreException>(() => context.Database.RestoreFrom(context.Database.Path));
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
