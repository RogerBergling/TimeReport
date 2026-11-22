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

    public static bool IsRegularFile(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() && TryStatx(path, out var information))
            {
                return (information.Mode & 0xF000) == 0x8000;
            }

            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) == 0 &&
                   (attributes & FileAttributes.Device) == 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? LinuxFileId(string path)
    {
        if (!TryStatx(path, out var information))
        {
            return null;
        }

        return $"{information.DevMajor}:{information.DevMinor}:{information.Inode}";
    }

    private static bool TryStatx(string path, out StatxBuffer information)
    {
        try
        {
            // AT_FDCWD, follow symlinks, request basic stats (type, inode, device).
            return Statx(-100, path, 0, 0x07ff, out information) == 0;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or
            EntryPointNotFoundException or
            PlatformNotSupportedException)
        {
            information = default;
            return false;
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
    private struct StatxTimestamp
    {
        public long Seconds;
        public uint Nanoseconds;
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StatxSpare
    {
        public ulong A;
        public ulong B;
        public ulong C;
        public ulong D;
        public ulong E;
        public ulong F;
        public ulong G;
        public ulong H;
        public ulong I;
    }

    // Linux struct statx. The spare fields keep the native buffer at 256 bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct StatxBuffer
    {
        public uint Mask;
        public uint BlockSize;
        public ulong Attributes;
        public uint LinkCount;
        public uint Uid;
        public uint Gid;
        public ushort Mode;
        public ushort Spare0;
        public ulong Inode;
        public ulong Size;
        public ulong Blocks;
        public ulong AttributesMask;
        public StatxTimestamp AccessTime;
        public StatxTimestamp BirthTime;
        public StatxTimestamp ChangeTime;
        public StatxTimestamp ModifyTime;
        public uint RdevMajor;
        public uint RdevMinor;
        public uint DevMajor;
        public uint DevMinor;
        public ulong MountId;
        public uint DirectIoMemoryAlign;
        public uint DirectIoOffsetAlign;
        public ulong Subvolume;
        public uint AtomicWriteUnitMin;
        public uint AtomicWriteUnitMax;
        public uint AtomicWriteSegmentsMax;
        public uint DirectIoReadOffsetAlign;
        public StatxSpare Spare3;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "statx")]
    private static extern int Statx(
        int directoryFileDescriptor,
        string path,
        int flags,
        uint mask,
        out StatxBuffer buffer);

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
