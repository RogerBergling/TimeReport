using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void ExplicitOverrideWins()
    {
        var expected = Path.Combine(Path.GetTempPath(), "timereport-override");
        Assert.Equal(
            Path.GetFullPath(expected),
            AppPaths.ResolveDataDir(expected));
    }

    [Fact]
    public void EnvironmentOverrideWins()
    {
        var expected = Path.Combine(Path.GetTempPath(), "timereport-env-override");
        var previous = Environment.GetEnvironmentVariable("TIMEREPORT_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("TIMEREPORT_DATA_DIR", expected);
            Assert.Equal(Path.GetFullPath(expected), AppPaths.ResolveDataDir());
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIMEREPORT_DATA_DIR", previous);
        }
    }

    [Fact]
    public void ExpandsHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(home, "timereport-home-test")),
            AppPaths.ResolveDataDir("~/timereport-home-test"));
    }

    [Fact]
    public void BuildsDatabaseBackupAndLogPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), "timereport-paths");
        Assert.Equal(Path.Combine(directory, "timereport.db"), AppPaths.DatabasePath(directory));
        Assert.Equal(Path.Combine(directory, "backups"), AppPaths.BackupsDir(directory));
        Assert.Equal(Path.Combine(directory, "timereport.log"), AppPaths.LogPath(directory));
    }

    [Fact]
    public void SettingsPathUsesConfigDirectory()
    {
        var path = AppPaths.SettingsPath();
        Assert.Equal("settings.json", Path.GetFileName(path));
        Assert.Equal("TimeReport", Path.GetFileName(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void DefaultDatabasePathEndsWithDatabaseFilename()
    {
        Assert.Equal("timereport.db", Path.GetFileName(AppPaths.DefaultDatabasePath()));
    }

    [Fact]
    public void EnsuresDataDirectoryExists()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"),
            "data");
        try
        {
            AppPaths.EnsureDataDir(directory);
            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void DefaultDirectoryUsesPlatformName()
    {
        var previous = Environment.GetEnvironmentVariable("TIMEREPORT_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("TIMEREPORT_DATA_DIR", null);
            var directory = AppPaths.ResolveDataDir();
            if (OperatingSystem.IsWindows())
            {
                Assert.EndsWith("TimeReport", directory);
            }
            else
            {
                Assert.EndsWith("timereport", directory);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIMEREPORT_DATA_DIR", previous);
        }
    }
}
