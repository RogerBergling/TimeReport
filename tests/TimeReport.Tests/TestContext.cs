using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class TestContext : IDisposable
{
    public TestContext()
    {
        Directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Database = new Database(Path.Combine(Directory, "timereport.db"));
        Database.Open();
        Service = new TimeReportService(Database);
    }

    public string Directory { get; }

    public Database Database { get; }

    public TimeReportService Service { get; }

    public void Dispose()
    {
        Database.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public static class TestData
{
    public static Entry Entry(
        DateOnly day,
        EntryKind kind,
        string start,
        string end,
        long? projectId = null,
        string? reason = null,
        long? id = null) =>
        new(
            id,
            day,
            kind,
            TimeUtil.ParseTime(start),
            TimeUtil.ParseTime(end),
            projectId,
            AwayReason: reason);

    public static readonly DateOnly Monday = new(2026, 10, 26);
    public static readonly DateOnly Tuesday = new(2026, 10, 27);
    public static readonly DateOnly Saturday = new(2026, 10, 31);
}
