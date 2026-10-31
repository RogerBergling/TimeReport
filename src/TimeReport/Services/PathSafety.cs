using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TimeReport.Services;

/// <summary>
/// Helpers for comparing file-system paths after resolving symlinks and
/// normalising platform-specific case rules.
/// </summary>
public static class PathSafety
{
    private static readonly string[] DatabaseSidecarSuffixes = ["-wal", "-shm", "-journal"];

    public static string Canonicalize(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                return fullPath;
            }

            var current = root;
            var remainder = fullPath[root.Length..];
            foreach (var part in remainder.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(current, part);
                FileSystemInfo info = Directory.Exists(candidate)
                    ? new DirectoryInfo(candidate)
                    : new FileInfo(candidate);
                if (info.Exists)
                {
                    var target = info.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is not null)
                    {
                        candidate = target.FullName;
                    }
                }
                current = candidate;
            }

            return Path.GetFullPath(current);
        }
        catch (IOException)
        {
            return fullPath;
        }
        catch (UnauthorizedAccessException)
        {
            return fullPath;
        }
    }

    public static bool AreSamePath(string first, string second) =>
        string.Equals(
            Canonicalize(first),
            Canonicalize(second),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    public static bool AreSameFile(string first, string second)
    {
        if (AreSamePath(first, second))
        {
            return true;
        }

        try
        {
            if (!File.Exists(first) || !File.Exists(second))
            {
                return false;
            }

            if (OperatingSystem.IsLinux())
            {
                var firstId = LinuxFileId(first);
                return firstId is not null && string.Equals(firstId, LinuxFileId(second), StringComparison.Ordinal);
            }
            if (OperatingSystem.IsWindows())
            {
                var firstId = WindowsFileId(first);
                return firstId is not null && string.Equals(firstId, WindowsFileId(second), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        return false;
    }

    private static string? LinuxFileId(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo("stat")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-Lc");
            startInfo.ArgumentList.Add("%d:%i");
            startInfo.ArgumentList.Add(path);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? WindowsFileId(string path)
    {
        using var handle = CreateFile(
            path,
            0x00000080,
            0x00000001 | 0x00000002 | 0x00000004,
            IntPtr.Zero,
            3,
            0x00000080,
            IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var information))
        {
            return null;
        }
        return $"{information.VolumeSerialNumber}:{information.FileIndexHigh}:{information.FileIndexLow}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    public static bool IsDatabaseSidecar(string path, string databasePath)
    {
        var candidate = Canonicalize(path);
        var database = Canonicalize(databasePath);
        return DatabaseSidecarSuffixes.Any(suffix =>
            string.Equals(
                candidate,
                database + suffix,
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal));
    }

    public static bool IsDatabaseFile(string path, string databasePath) =>
        AreSamePath(path, databasePath) || IsDatabaseSidecar(path, databasePath);
}
