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
    public void EnsureDirectoryDoesNotHardenAnExistingDirectory()
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
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);

            AppPaths.EnsureDirectory(directory);

            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute,
                File.GetUnixFileMode(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CleanupTemporaryFilesRemovesOnlyTimeReportSnapshots()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            AppPaths.MarkAppDataDirectory(directory);
            var tempDir = AppPaths.TempDir(directory);
            Directory.CreateDirectory(tempDir);
            var staleRoot = Path.Combine(directory, ".dropbox-upload-stale.db");
            var staleTemp = Path.Combine(tempDir, ".dropbox-download-stale.db.plain.db");
            var staleExport = Path.Combine(tempDir, ".timereport-export-stale.xlsx");
            var unrelated = Path.Combine(directory, "notes.txt");
            var unrelatedTemp = Path.Combine(tempDir, "user-file.txt");
            File.WriteAllText(staleRoot, "stale");
            File.WriteAllText(staleTemp, "stale");
            File.WriteAllText(staleExport, "stale");
            File.WriteAllText(unrelated, "keep");
            File.WriteAllText(unrelatedTemp, "keep");

            AppPaths.CleanupTemporaryFiles(directory);

            Assert.False(File.Exists(staleRoot));
            Assert.False(File.Exists(staleTemp));
            Assert.False(File.Exists(staleExport));
            Assert.True(File.Exists(unrelated));
            Assert.True(File.Exists(unrelatedTemp));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CleanupTemporaryFilesSkipsUnmarkedDirectories()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var tempDir = AppPaths.TempDir(directory);
            Directory.CreateDirectory(tempDir);
            var staleRoot = Path.Combine(directory, ".dropbox-upload-stale.db");
            var staleTemp = Path.Combine(tempDir, ".dropbox-download-stale.db.plain.db");
            var unrelatedTemp = Path.Combine(tempDir, "user-file.txt");
            File.WriteAllText(staleRoot, "stale");
            File.WriteAllText(staleTemp, "stale");
            File.WriteAllText(unrelatedTemp, "keep");

            AppPaths.CleanupTemporaryFiles(directory);

            Assert.True(File.Exists(staleRoot));
            Assert.True(File.Exists(staleTemp));
            Assert.True(File.Exists(unrelatedTemp));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EnsureDataDirDoesNotHardenExistingUnmarkedDirectory()
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
            var mode =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute;
            File.SetUnixFileMode(directory, mode);

            AppPaths.EnsureDataDir(directory);

            Assert.Equal(mode, File.GetUnixFileMode(directory));
            Assert.False(File.Exists(
                Path.Combine(directory, AppPaths.DataDirectoryMarkerFileName)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EnsureDataDirMarksNewAppDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"),
            "data");
        try
        {
            AppPaths.EnsureDataDir(directory);

            Assert.True(File.Exists(
                Path.Combine(directory, AppPaths.DataDirectoryMarkerFileName)));
            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
            Assert.True(AppPaths.IsAppOwnedDataDir(directory));
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
