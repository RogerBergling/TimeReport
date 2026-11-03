using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void LockFileRemainsAfterReleaseAndCanBeReacquired()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "timereport.lock");
        try
        {
            using (var first = SingleInstanceGuard.Acquire(path))
            {
                Assert.True(File.Exists(path));
            }

            Assert.True(File.Exists(path));
            using var second = SingleInstanceGuard.Acquire(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
