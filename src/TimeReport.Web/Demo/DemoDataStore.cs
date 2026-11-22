using TimeReport.Web.Configuration;

namespace TimeReport.Web.Demo;

public static class DemoDataStore
{
    private static readonly string[] DataFiles =
    [
        "auth.db",
        "auth.db-wal",
        "auth.db-shm",
        "auth.db-journal",
        "timereport.db",
        "timereport.db-wal",
        "timereport.db-shm",
        "timereport.db-journal",
    ];

    /// <summary>
    /// Removes only TimeReport-owned database state from the data directory.
    /// The demo image uses a dedicated tmpfs, but this also protects a demo
    /// container that was started with a persistent volume by mistake.
    /// </summary>
    public static void ResetForDemo(string dataDirectory)
    {
        var fullPath = Path.GetFullPath(dataDirectory);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) ||
            string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to reset demo data at a filesystem root.");
        }

        WebPaths.EnsurePrivateDirectory(fullPath);
        foreach (var file in DataFiles)
        {
            var path = Path.Combine(fullPath, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        DeleteDirectory(Path.Combine(fullPath, "users"));
        DeleteDirectory(Path.Combine(fullPath, "backups"));
        DeleteDirectory(Path.Combine(fullPath, "temp"));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
