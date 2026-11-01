using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class OvertimeTests
{
    [Fact]
    public void DailyOvertimeUsesConfiguredThreshold()
    {
        using var context = new TestContext();
        context.Service.OvertimePolicy = new OvertimePolicy(480, 0);
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));

        Assert.Equal(30, context.Service.DailyTotals(TestData.Monday).OvertimeMinutes);
        Assert.Equal(30, context.Service.RangeTotals(TestData.Monday, TestData.Monday).OvertimeMinutes);
    }

    [Fact]
    public void WeeklyOvertimeAddsOnlyTimeAboveTheWeeklyThreshold()
    {
        using var context = new TestContext();
        context.Service.OvertimePolicy = new OvertimePolicy(0, 2400);
        var project = context.Service.CreateProject("Acme");
        for (var offset = 0; offset < 5; offset++)
        {
            var day = TestData.Monday.AddDays(offset);
            context.Service.SaveEntry(TestData.Entry(
                day, EntryKind.Work, "08:00", "17:00", project.Id));
            context.Service.SaveEntry(TestData.Entry(
                day, EntryKind.Lunch, "12:00", "12:30"));
        }

        var totals = context.Service.RangeTotals(
            TestData.Monday,
            TestData.Monday.AddDays(4));

        Assert.Equal(2550, totals.WorkedMinutes);
        Assert.Equal(150, totals.OvertimeMinutes);
    }
}
