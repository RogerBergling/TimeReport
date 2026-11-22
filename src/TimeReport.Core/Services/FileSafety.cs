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

    private static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024
            ? $"{bytes / (1024L * 1024 * 1024)} GiB"
            : $"{bytes / (1024L * 1024)} MiB";
}
