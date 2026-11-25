namespace TimeReport.Services;

/// <summary>
/// Size and file-type checks for files that are read from user-controlled or
/// network-controlled locations. These guards stop the app from following
/// devices, FIFOs or unexpectedly large files into parsers and databases.
/// </summary>
public static class FileSafety
{
    public const long MaxSettingsBytes = 1024 * 1024;
    public const long MaxKeyFileBytes = 64 * 1024;
    public const long MaxCertificateBytes = 1024 * 1024;
    public const long MaxDatabaseBytes = 8L * 1024 * 1024 * 1024;

    public static void EnsureRegularFile(string path, long maxBytes, string description)
    {
        if (!File.Exists(path))
        {
            throw new IOException($"{description} does not exist.");
        }
        if (!PathSafety.IsRegularFile(path))
        {
            throw new IOException($"{description} is not a regular file.");
        }

        var length = new FileInfo(path).Length;
        if (length > maxBytes)
        {
            throw new IOException(
                $"{description} is too large (maximum {FormatBytes(maxBytes)}).");
        }
    }

    /// <summary>
    /// A settings key restored from a backup or copied between machines is
    /// often mode 0644. Refuse it instead of silently using a key that another
    /// local user can read. Windows uses DPAPI rather than a raw key file.
    /// </summary>
    public static void EnsureOwnerOnlyFile(string path, long maxBytes, string description)
    {
        EnsureRegularFile(path, maxBytes, description);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode forbidden =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        var mode = File.GetUnixFileMode(path);
        if ((mode & forbidden) != 0)
        {
            throw new IOException(
                $"{description} must be owner-only (0600); found {mode}. " +
                "Run: chmod 600 <file>");
        }
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024
            ? $"{bytes / (1024L * 1024 * 1024)} GiB"
            : $"{bytes / (1024L * 1024)} MiB";
}
