namespace TimeReport.Services;

public static class AppPaths
{
    public static string ResolveDataDir(string? overrideDataDir = null)
    {
        var value = overrideDataDir ?? Environment.GetEnvironmentVariable("TIMEREPORT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(ExpandHome(value.Trim()));
        }

        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.GetFullPath(Path.Combine(local, "TimeReport"));
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.GetFullPath(Path.Combine(home, "Library", "Application Support", "TimeReport"));
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.GetFullPath(Path.Combine(baseDir, "timereport"));
    }

    public static string DatabasePath(string dataDir) => Path.Combine(dataDir, "timereport.db");

    public static string DefaultDatabasePath() => DatabasePath(ResolveDataDir());

    public static string BackupsDir(string dataDir) => Path.Combine(dataDir, "backups");

    public static string TempDir(string dataDir) => Path.Combine(dataDir, "temp");

    public static string LogPath(string dataDir) => Path.Combine(dataDir, "timereport.log");

    public static string ConfigDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "TimeReport");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "TimeReport");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(baseDir, "TimeReport");
    }

    public static string SettingsPath() => Path.Combine(ConfigDir(), "settings.json");

    /// <summary>
    /// Creates an application-owned directory and keeps it private to the current user.
    /// Do not use this for arbitrary user-selected database parents.
    /// </summary>
    public static void EnsureDataDir(string dataDir)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(dataDir);
            return;
        }

        Directory.CreateDirectory(
            dataDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            File.SetUnixFileMode(
                dataDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Creates a directory without changing permissions on an existing directory.
    /// Used for user-selected database parents and export locations.
    /// </summary>
    public static void EnsureDirectory(string directory) =>
        Directory.CreateDirectory(directory);

    /// <summary>
    /// Best-effort cleanup of stale plaintext snapshots left by an interrupted sync
    /// or export. Only TimeReport-specific temporary names are removed.
    /// </summary>
    public static void CleanupTemporaryFiles(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            return;
        }

        DeleteMatching(dataDir, ".dropbox-upload-*.db");
        DeleteMatching(dataDir, ".dropbox-download-*.db");
        DeleteMatching(dataDir, ".dropbox-download-*.db.plain.db");
        DeleteMatching(dataDir, ".timereport-plain-*.db");
        DeleteMatching(dataDir, ".timereport-export-*.xlsx");

        var tempDir = TempDir(dataDir);
        if (!Directory.Exists(tempDir))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(tempDir))
            {
                TryDelete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteMatching(string directory, string pattern)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern))
            {
                TryDelete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void SecureFile(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static string TimestampForFilename() =>
        DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);

    public static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }
        return Environment.ExpandEnvironmentVariables(path);
    }
}
