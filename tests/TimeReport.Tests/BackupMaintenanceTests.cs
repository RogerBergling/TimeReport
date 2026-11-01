using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class BackupMaintenanceTests
{
    [Fact]
    public void PruneBackupsKeepsNewestAutomaticBackups()
    {
        using var directory = new TempDirectory();
        var backups = Path.Combine(directory.Path, "backups");
        Directory.CreateDirectory(backups);
        for (var index = 0; index < 25; index++)
        {
            var path = Path.Combine(backups, $"pre-restore-{index:00}.db");
            File.WriteAllText(path, "x");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-index));
        }
        File.WriteAllText(Path.Combine(backups, "manual.db"), "x");

        var removed = Database.PruneBackups(directory.Path, 20);

        Assert.Equal(5, removed);
        Assert.Equal(20, Directory.EnumerateFiles(backups, "pre-restore-*.db").Count());
        Assert.True(File.Exists(Path.Combine(backups, "manual.db")));
    }

    [Fact]
    public void IntegrityCheckCompactAndRepairWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "16:00", project.Id));

        Assert.Equal("ok", context.Database.IntegrityCheck());
        context.Database.Compact();
        Assert.Equal("ok", context.Database.IntegrityCheck());

        var backup = context.Database.Repair();
        Assert.True(File.Exists(backup));
        Assert.Equal("ok", context.Database.IntegrityCheck());
        Assert.Single(context.Service.ListEntries(TestData.Monday, TestData.Monday));
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
