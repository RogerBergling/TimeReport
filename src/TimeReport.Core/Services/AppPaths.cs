namespace TimeReport.Services;

public static class AppPaths
{
    public const string DataDirectoryMarkerFileName = ".timereport-data";
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly string[] TemporaryFilePatterns =
    [
        ".dropbox-upload-*.db",
        ".dropbox-upload-*.db.enc",
        ".dropbox-download-*.db",
        ".dropbox-download-*.db.plain.db",
        ".timereport-plain-*.db",
        ".timereport-export-*.xlsx",
        ".timereport-backup-*.db",
        ".timereport-restore-*.db",
    ];

    public static string ResolveDataDir(string? overrideDataDir = null)
    {
        var value = overrideDataDir ?? Environment.GetEnvironmentVariable("TIMEREPORT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(ExpandHome(value.Trim()));
        }

        return DefaultDataDir();
    }

    public static string DefaultDataDir()
    {
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

    public static string ConfigDir() => DefaultConfigDir();

    public static string DefaultConfigDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.GetFullPath(Path.Combine(appData, "TimeReport"));
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.GetFullPath(Path.Combine(home, "Library", "Application Support", "TimeReport"));
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.GetFullPath(Path.Combine(baseDir, "TimeReport"));
    }

    public static string SettingsPath() => Path.Combine(ConfigDir(), "settings.json");

    /// <summary>
    /// True when the directory is the default application data/config directory or
    /// was explicitly marked by TimeReport. Cleanup and permission hardening only
    /// touch these directories, never arbitrary user-selected parents.
    /// </summary>
    public static bool IsAppOwnedDataDir(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            return false;
        }

        var candidate = PathSafety.Canonicalize(dataDir);
        return PathsEqual(candidate, PathSafety.Canonicalize(DefaultDataDir())) ||
               PathsEqual(candidate, PathSafety.Canonicalize(DefaultConfigDir())) ||
               File.Exists(Path.Combine(candidate, DataDirectoryMarkerFileName));
    }

    public static void MarkAppDataDirectory(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir) || !Directory.Exists(dataDir))
        {
            return;
        }

        try
        {
            WriteAllBytesPrivate(
                Path.Combine(dataDir, DataDirectoryMarkerFileName),
                "TimeReport data directory\n"u8.ToArray());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Creates an application-owned directory and keeps it private to the current user.
    /// Existing unrelated directories are left untouched; only the default app data
    /// directory or a directory already marked as app-owned is hardened.
    /// Do not use this for arbitrary user-selected database parents.
    /// </summary>
    public static void EnsureDataDir(string dataDir)
    {
        var fullPath = Path.GetFullPath(dataDir);
        var existed = Directory.Exists(fullPath);
        if (!existed && !OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(fullPath, PrivateDirectoryMode);
        }
        else
        {
            Directory.CreateDirectory(fullPath);
        }

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (!existed || IsAppOwnedDataDir(fullPath))
        {
            TryHardenDirectory(fullPath);
            MarkAppDataDirectory(fullPath);
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
    /// or export. Only TimeReport-specific temporary names are removed, and only
    /// from app-owned directories.
    /// </summary>
    public static void CleanupTemporaryFiles(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir) || !IsAppOwnedDataDir(dataDir))
        {
            return;
        }

        foreach (var pattern in TemporaryFilePatterns)
        {
            DeleteMatching(dataDir, pattern);
        }

        var tempDir = TempDir(Path.GetFullPath(dataDir));
        if (!Directory.Exists(tempDir))
        {
            return;
        }

        // Never follow a temp directory symlink out of the app-owned data folder.
        var canonicalDataDir = PathSafety.Canonicalize(dataDir);
        var canonicalTempDir = PathSafety.Canonicalize(tempDir);
        var parent = Path.GetDirectoryName(canonicalTempDir);
        if (!string.Equals(
                parent,
                canonicalDataDir,
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            return;
        }

        foreach (var pattern in TemporaryFilePatterns)
        {
            DeleteMatching(tempDir, pattern);
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
            File.SetUnixFileMode(path, PrivateFileMode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Opens or creates a file with owner-only permissions on Unix. The mode is
    /// applied at creation time so the file is never briefly world-readable.
    /// </summary>
    public static void EnsurePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            return;
        }

        using (CreatePrivateFileStream(
                   path,
                   FileMode.OpenOrCreate,
                   FileAccess.ReadWrite,
                   FileShare.ReadWrite))
        {
        }
        SecureFile(path);
    }

    public static FileStream CreatePrivateFileStream(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share)
    {
        var options = new FileStreamOptions
        {
            Mode = mode,
            Access = access,
            Share = share,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }
        return new FileStream(path, options);
    }

    public static FileStream OpenPrivateAppendStream(string path) =>
        CreatePrivateFileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);

    public static void WriteAllBytesPrivate(string path, byte[] bytes)
    {
        using (var stream = CreatePrivateFileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        SecureFile(path);
    }

    private static void TryHardenDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(directory, PrivateDirectoryMode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

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
