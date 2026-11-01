using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class TimeZoneTests
{
    [Fact]
    public void UtcIsResolvedAndValid()
    {
        Assert.True(TimeZoneService.IsValid("UTC"));
        Assert.Equal("UTC", TimeZoneService.ResolveId("UTC"));
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.UtcNow),
            TimeZoneService.Today("UTC"));
    }

    [Fact]
    public void EntryTimeZoneRoundTrips()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var saved = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "16:00",
            project.Id) with
        {
            TimeZoneId = "UTC",
        });

        Assert.Equal("UTC", saved.TimeZoneId);
        Assert.Equal("UTC", context.Service.GetEntry(saved.Id!.Value)!.TimeZoneId);
    }

    [Fact]
    public void InvalidTimeZoneFallsBackToLocal()
    {
        Assert.Equal(
            TimeZoneInfo.Local.Id,
            TimeZoneService.ResolveId("Not/A_Real_Zone"));
    }
}
