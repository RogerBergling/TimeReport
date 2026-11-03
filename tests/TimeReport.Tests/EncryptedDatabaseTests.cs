using System.Security.Cryptography;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class EncryptedDatabaseTests
{
    [Fact]
    public void EncryptedDatabaseIsNotPlaintextAndReopensWithKey()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "encrypted.db");
        var key = NewKey();

        using (var database = new Database(path, key))
        {
            database.Open();
            var service = new TimeReportService(database);
            var project = service.CreateProject("Acme");
            service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));
        }

        Assert.False(IsPlaintext(path));

        using (var database = new Database(path, key))
        {
            database.Open();
            var service = new TimeReportService(database);
            Assert.Equal(["Acme"], service.ListProjects().Select(project => project.Name));
            Assert.Single(service.ListEntries(TestData.Monday, TestData.Monday));
        }
    }

    [Fact]
    public void WrongKeyIsRejected()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "encrypted.db");
        var key = NewKey();

        using (var database = new Database(path, key))
        {
            database.Open();
        }

        using var wrong = new Database(path, NewKey());
        Assert.Throws<DatabaseException>(() => wrong.Open());
    }

    [Fact]
    public void PlaintextDatabaseIsMigratedToEncrypted()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "plain.db");
        using (var plain = new Database(path))
        {
            plain.Open();
            var service = new TimeReportService(plain);
            var project = service.CreateProject("Legacy");
            service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));
        }
        Assert.True(IsPlaintext(path));

        var key = NewKey();
        using (var encrypted = new Database(path, key))
        {
            encrypted.Open();
            var service = new TimeReportService(encrypted);
            Assert.Equal(["Legacy"], service.ListProjects().Select(project => project.Name));
        }

        Assert.False(IsPlaintext(path));
        var backupPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(directory.Path, "backups"),
            "pre-encryption-*.db"));
        Assert.False(IsPlaintext(backupPath));
        Assert.Equal(Database.SchemaVersion, Database.ValidateFile(backupPath, key));
        using var backup = new Database(backupPath, key);
        backup.Open();
        Assert.Equal(["Legacy"], new TimeReportService(backup).ListProjects().Select(project => project.Name));
    }

    [Fact]
    public void EncryptedBackupCanBeValidatedAndReopened()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "live.db");
        var backupPath = Path.Combine(directory.Path, "backup.db");
        var key = NewKey();

        using (var database = new Database(path, key))
        {
            database.Open();
            var service = new TimeReportService(database);
            service.CreateProject("Acme");
            database.BackupTo(backupPath);
        }

        Assert.False(IsPlaintext(backupPath));
        Assert.Equal(Database.SchemaVersion, Database.ValidateFile(backupPath, key));
        using var backup = new Database(backupPath, key);
        backup.Open();
        Assert.Equal(["Acme"], new TimeReportService(backup).ListProjects().Select(project => project.Name));
    }

    [Fact]
    public void EncryptedDatabaseCanCreatePlaintextSyncSnapshot()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "live.db");
        var plainPath = Path.Combine(directory.Path, "snapshot.db");
        var key = NewKey();

        using (var database = new Database(path, key))
        {
            database.Open();
            new TimeReportService(database).CreateProject("Acme");
            database.BackupToPlaintext(plainPath);
        }

        Assert.True(IsPlaintext(plainPath));
        Assert.Equal(Database.SchemaVersion, Database.ValidateFile(plainPath));
        using var plain = new Database(plainPath);
        plain.Open();
        Assert.Equal(["Acme"], new TimeReportService(plain).ListProjects().Select(project => project.Name));
    }

    [Fact]
    public void PlaintextRestoreIsReencryptedIntoLiveDatabase()
    {
        using var directory = new TempDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.db");
        var livePath = Path.Combine(directory.Path, "live.db");
        var key = NewKey();

        using (var source = new Database(sourcePath))
        {
            source.Open();
            new TimeReportService(source).CreateProject("Restored");
        }

        using (var live = new Database(livePath, key))
        {
            live.Open();
            new TimeReportService(live).CreateProject("Current");
            live.RestoreFrom(sourcePath);
            Assert.Equal(["Restored"], new TimeReportService(live).ListProjects().Select(project => project.Name));
        }

        Assert.False(IsPlaintext(livePath));
    }

    private static string NewKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static bool IsPlaintext(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        return stream.Read(header) == header.Length &&
               header.SequenceEqual("SQLite format 3\0"u8);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
