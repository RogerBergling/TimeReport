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

    public static string BackupsDir(string dataDir) => Path.Combine(dataDir, "backups");

    public static string LogPath(string dataDir) => Path.Combine(dataDir, "timereport.log");

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

    private static string ExpandHome(string path)
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
