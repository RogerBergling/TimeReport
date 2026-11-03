using System.Globalization;

namespace TimeReport.Services;

public sealed class SingleInstanceException : Exception
{
    public SingleInstanceException(string message) : base(message)
    {
    }

    public SingleInstanceException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Holds an exclusive lock on a small lock file for the lifetime of the app.
/// The file itself may remain after a crash; the operating-system lock is what
/// prevents a second instance from opening the same settings/database.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private static readonly HashSet<string> HeldPaths = new(
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
    private static readonly object HeldSync = new();
    private readonly string _path;
    private FileStream? _stream;

    private SingleInstanceGuard(string path, FileStream stream)
    {
        _path = path;
        _stream = stream;
    }

    public static SingleInstanceGuard Acquire(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            AppPaths.EnsureDataDir(directory);
        }

        lock (HeldSync)
        {
            if (HeldPaths.Contains(fullPath))
            {
                throw new SingleInstanceException(
                    "TimeReport is already running. Close the other window and try again.");
            }
            HeldPaths.Add(fullPath);
        }

        try
        {
            var stream = new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            try
            {
                Lock(stream);
                var pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
                stream.SetLength(0);
                using (var writer = new StreamWriter(
                           stream,
                           System.Text.Encoding.UTF8,
                           bufferSize: 1024,
                           leaveOpen: true))
                {
                    writer.Write(pid);
                }
                stream.Flush();
                return new SingleInstanceGuard(fullPath, stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (IOException exception)
        {
            ReleasePath(fullPath);
            throw new SingleInstanceException(
                "TimeReport is already running. Close the other window and try again.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReleasePath(fullPath);
            throw new SingleInstanceException(
                "TimeReport could not create its instance lock file.",
                exception);
        }
        catch
        {
            ReleasePath(fullPath);
            throw;
        }
    }

    private static void ReleasePath(string path)
    {
        lock (HeldSync)
        {
            HeldPaths.Remove(path);
        }
    }

    private static void Lock(FileStream stream)
    {
        if (OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Single-instance locking is not supported on macOS.");
        }
        stream.Lock(0, 1);
    }

    private static void Unlock(FileStream stream)
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }
        stream.Unlock(0, 1);
    }

    public void Dispose()
    {
        var stream = _stream;
        _stream = null;
        if (stream is null)
        {
            return;
        }

        try
        {
            Unlock(stream);
        }
        catch (IOException)
        {
        }
        stream.Dispose();
        ReleasePath(_path);
        // Keep the lock file. Deleting it after unlocking creates a race where a
        // second instance can acquire the old file while a third creates a new one.
    }
}
