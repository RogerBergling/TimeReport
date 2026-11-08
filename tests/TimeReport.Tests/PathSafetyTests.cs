using System.Runtime.InteropServices;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class PathSafetyTests
{
    [Fact]
    public void DistinguishesRegularFilesFromDirectories()
    {
        using var directory = new TempDirectory();
        var file = Path.Combine(directory.Path, "file.txt");
        File.WriteAllText(file, "data");

        Assert.True(PathSafety.IsRegularFile(file));
        Assert.False(PathSafety.IsRegularFile(directory.Path));
        Assert.False(PathSafety.IsRegularFile(Path.Combine(directory.Path, "missing")));
    }

    [Fact]
    public void DetectsSymbolicLinkAliases()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var file = Path.Combine(directory.Path, "file.txt");
        var alias = Path.Combine(directory.Path, "alias.txt");
        File.WriteAllText(file, "data");
        File.CreateSymbolicLink(alias, file);

        Assert.True(PathSafety.AreSameFile(file, alias));
        Assert.True(PathSafety.AreSamePath(file, alias));
    }

    [Fact]
    public void DetectsHardLinkAliasesWithoutExternalStat()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TempDirectory();
        var file = Path.Combine(directory.Path, "file.txt");
        var alias = Path.Combine(directory.Path, "hardlink.txt");
        File.WriteAllText(file, "data");
        Assert.Equal(0, LinkExisting(
            System.Text.Encoding.UTF8.GetBytes(file + "\0"),
            System.Text.Encoding.UTF8.GetBytes(alias + "\0")));

        Assert.True(PathSafety.AreSameFile(file, alias));
        Assert.False(PathSafety.AreSamePath(file, alias));
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "link")]
    private static extern int LinkExisting(byte[] existingPath, byte[] newPath);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
