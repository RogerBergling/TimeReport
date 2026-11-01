namespace TimeReport.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void VersionAndBuildNumberAreAvailable()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.BuildNumber));
    }
}
