using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class AppLogTests
{
    [Fact]
    public void LogFileIsOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            AppLog.Initialize(directory);
            AppLog.Info("permissions check");

            var mode = File.GetUnixFileMode(Path.Combine(directory, "timereport.log"));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public void WritesAndRotatesLogFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            AppLog.Initialize(directory, maxBytes: 80);
            AppLog.Info(new string('a', 100));
            AppLog.Info(new string('b', 100));

            Assert.True(File.Exists(Path.Combine(directory, "timereport.log")));
            Assert.True(File.Exists(Path.Combine(directory, "timereport.log.1")));
            Assert.Equal(
                Path.Combine(directory, "timereport.log"),
                AppLog.CurrentPath);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
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
