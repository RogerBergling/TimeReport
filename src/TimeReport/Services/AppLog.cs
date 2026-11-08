using System.Globalization;

namespace TimeReport.Services;

/// <summary>
/// Small, dependency-free rolling file logger. Logging must never take the
/// application down, so every file operation is best-effort.
/// </summary>
public static class AppLog
{
    private const long DefaultMaxBytes = 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _path;
    private static long _maxBytes = DefaultMaxBytes;

    public static string? CurrentPath => _path;

    public static void Initialize(string dataDir, long maxBytes = DefaultMaxBytes)
    {
        lock (Sync)
        {
            try
            {
                AppPaths.EnsureDirectory(dataDir);
                _path = AppPaths.LogPath(dataDir);
                _maxBytes = maxBytes <= 0 ? DefaultMaxBytes : maxBytes;
                AppPaths.EnsurePrivateFile(_path);
                RotateIfNeeded();
            }
            catch (Exception)
            {
                _path = null;
            }
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) =>
        Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        lock (Sync)
        {
            if (string.IsNullOrWhiteSpace(_path))
            {
                return;
            }

            try
            {
                RotateIfNeeded();
                var timestamp = DateTimeOffset.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss zzz",
                    CultureInfo.InvariantCulture);
                var line = $"{timestamp} [{level}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }
                using (var stream = AppPaths.OpenPrivateAppendStream(_path))
                using (var writer = new StreamWriter(
                           stream,
                           new System.Text.UTF8Encoding(false)))
                {
                    writer.Write(line + Environment.NewLine);
                }
                AppPaths.SecureFile(_path);
            }
            catch (Exception)
            {
                // Diagnostics must not break normal operation.
            }
        }
    }

    private static void RotateIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
        {
            return;
        }
        if (new FileInfo(_path).Length < _maxBytes)
        {
            return;
        }
        File.Move(_path, _path + ".1", true);
    }
}
