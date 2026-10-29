using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class TotalsTests
{
    [Fact]
    public void WorkLunchAndXPauseTotals()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:15"));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(510, totals.WorkedMinutes);
        Assert.Equal(30, totals.LunchMinutes);
        Assert.Equal(15, totals.XPauseMinutes);
        Assert.Equal(0, totals.AwayMinutes);
        Assert.Equal(1, totals.DaysWorked);
        Assert.Equal(0, totals.DaysAway);
    }

    [Fact]
    public void XPauseIsNotDeductedFromWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:15"));
        Assert.Equal(540, context.Service.DailyTotals(TestData.Monday).WorkedMinutes);
    }

    [Fact]
    public void SplitWorkAroundLunch()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "12:30", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(510, totals.WorkedMinutes);
        Assert.Equal(30, totals.LunchMinutes);
    }

    [Fact]
    public void LunchOutsideWorkDoesNotReduceWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(240, totals.WorkedMinutes);
        Assert.Equal(30, totals.LunchMinutes);
    }

    [Fact]
    public void LunchCanCoverAllWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "09:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "08:00", "09:00"));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(0, totals.WorkedMinutes);
        Assert.Equal(60, totals.LunchMinutes);
        Assert.Equal(0, totals.DaysWorked);
    }

    [Fact]
    public void AwayAndWorkOnSameDay()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Away, "09:00", "12:00", reason: "Sick"));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "13:00", "17:00", project.Id));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(240, totals.WorkedMinutes);
        Assert.Equal(180, totals.AwayMinutes);
        Assert.Equal(1, totals.DaysWorked);
        Assert.Equal(1, totals.DaysAway);
    }

    [Fact]
    public void DaysWorkedAndAwayAreCountedOnce()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "13:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Tuesday, EntryKind.Work, "08:00", "16:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Tuesday, EntryKind.Away, "16:00", "17:00", reason: "Appointment"));

        var totals = context.Service.RangeTotals(TestData.Monday, TestData.Tuesday);
        Assert.Equal(2, totals.DaysWorked);
        Assert.Equal(1, totals.DaysAway);
        Assert.Equal(960, totals.WorkedMinutes);
    }

    [Fact]
    public void WeekMonthYearAndAllTime()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Tuesday, EntryKind.Work, "08:00", "16:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(new DateOnly(2026, 11, 2), EntryKind.Work, "08:00", "10:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(new DateOnly(2027, 1, 4), EntryKind.Work, "08:00", "11:00", project.Id));

        Assert.Equal(720, context.Service.RangeTotals(TestData.Monday, TestData.Monday.AddDays(6)).WorkedMinutes);
        Assert.Equal(720, context.Service.RangeTotals(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).WorkedMinutes);
        Assert.Equal(840, context.Service.RangeTotals(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).WorkedMinutes);
        Assert.Equal(1020, context.Service.AllTimeTotals().WorkedMinutes);
        Assert.Equal((TestData.Monday, new DateOnly(2027, 1, 4)), context.Service.AllTimeBounds());

        var months = context.Service.TotalsByMonth(2026);
        Assert.Equal(12, months.Count);
        Assert.Equal(720, months[9].Totals.WorkedMinutes);
        Assert.Equal(120, months[10].Totals.WorkedMinutes);

        var years = context.Service.TotalsByYear(2026, 2027);
        Assert.Equal(2, years.Count);
        Assert.Equal(840, years[0].Totals.WorkedMinutes);
        Assert.Equal(180, years[1].Totals.WorkedMinutes);
    }

    [Fact]
    public void WeekIncludesMonthBoundary()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(new DateOnly(2026, 11, 1), EntryKind.Work, "08:00", "10:00", project.Id));
        Assert.Equal(360, context.Service.RangeTotals(TestData.Monday, TestData.Monday.AddDays(6)).WorkedMinutes);
    }

    [Fact]
    public void TotalsByDayIncludesEmptyDays()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));

        var rows = context.Service.TotalsByDay(TestData.Monday, TestData.Monday.AddDays(6));
        Assert.Equal(7, rows.Count);
        Assert.Equal(TestData.Monday, rows[0].Date);
        Assert.Equal(240, rows[0].Totals.WorkedMinutes);
        Assert.Equal(0, rows[1].Totals.WorkedMinutes);
        Assert.Single(context.Service.TotalsByDay(TestData.Monday, TestData.Monday.AddDays(6), false));
    }

    [Fact]
    public void EmptyDatabaseTotals()
    {
        using var context = new TestContext();
        Assert.Null(context.Service.AllTimeBounds());
        Assert.Equal(0, context.Service.AllTimeTotals().WorkedMinutes);
        Assert.Equal(0, context.Service.RangeTotals(TestData.Monday, TestData.Tuesday).WorkedMinutes);
    }

    [Fact]
    public void XPauseUsageAndRemaining()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:15"));
        Assert.Equal(15, context.Service.XPauseUsedInWeek(TestData.Monday));
        Assert.Equal(45, context.Service.XPauseRemaining(TestData.Monday));
    }
}
