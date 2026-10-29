using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class TimeUtilTests
{
    [Theory]
    [InlineData("08:00", 480)]
    [InlineData("8:00", 480)]
    [InlineData("00:00", 0)]
    [InlineData("23:59", 1439)]
    [InlineData("24:00", 1440)]
    public void ParseTimeAcceptsValidValues(string text, int expected)
    {
        Assert.Equal(expected, TimeUtil.ParseTime(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("25:00")]
    [InlineData("24:01")]
    [InlineData("12:60")]
    [InlineData("12:0a")]
    [InlineData("-1:00")]
    public void ParseTimeRejectsInvalidValues(string text)
    {
        Assert.Throws<ValidationException>(() => TimeUtil.ParseTime(text));
    }

    [Fact]
    public void ParseTimeCanRejectEndOfDayForStart()
    {
        Assert.Throws<ValidationException>(() => TimeUtil.ParseTime("24:00", false));
    }

    [Fact]
    public void FormatsTimeAndDuration()
    {
        Assert.Equal("00:00", TimeUtil.FormatTime(0));
        Assert.Equal("08:00", TimeUtil.FormatTime(480));
        Assert.Equal("24:00", TimeUtil.FormatTime(1440));
        Assert.Equal("0:00", TimeUtil.FormatDuration(0));
        Assert.Equal("1:05", TimeUtil.FormatDuration(65));
        Assert.Equal("25:00", TimeUtil.FormatDuration(1500));
        Assert.Throws<ValidationException>(() => TimeUtil.FormatDuration(-1));
    }

    [Fact]
    public void ParsesIsoDates()
    {
        Assert.Equal(new DateOnly(2026, 10, 26), TimeUtil.ParseDate("2026-10-26"));
        Assert.Throws<ValidationException>(() => TimeUtil.ParseDate("26/10/2026"));
    }

    [Fact]
    public void WeekStartsOnMonday()
    {
        var monday = new DateOnly(2026, 10, 26);
        var sunday = new DateOnly(2026, 11, 1);
        Assert.Equal(monday, TimeUtil.WeekStart(monday));
        Assert.Equal(monday, TimeUtil.WeekStart(sunday));
        Assert.Equal(sunday, TimeUtil.WeekEnd(monday));
        Assert.Equal(44, TimeUtil.WeekNumber(monday));
    }

    [Fact]
    public void CalculatesMonthYearAndAddMonths()
    {
        Assert.Equal(
            (new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 29)),
            TimeUtil.MonthBounds(new DateOnly(2024, 2, 10)));
        Assert.Equal(
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            TimeUtil.YearBounds(new DateOnly(2026, 10, 29)));
        Assert.Equal(new DateOnly(2026, 2, 28), TimeUtil.AddMonths(new DateOnly(2026, 1, 31), 1));
        Assert.Equal(new DateOnly(2024, 2, 29), TimeUtil.AddMonths(new DateOnly(2024, 1, 31), 1));
    }

    [Fact]
    public void FormatsDates()
    {
        var day = new DateOnly(2026, 10, 26);
        Assert.Equal("Monday 26 October 2026", TimeUtil.FormatDateLong(day));
        Assert.Equal("Mon 26 Oct", TimeUtil.FormatDayShort(day));
        Assert.Equal("26 Oct – 1 Nov 2026", TimeUtil.FormatWeekRange(day, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void MergesAndSubtractsIntervals()
    {
        Assert.Equal([(480, 780)], TimeUtil.MergeIntervals([(480, 720), (600, 780)]));
        Assert.Equal([(480, 720)], TimeUtil.MergeIntervals([(480, 600), (600, 720)]));
        Assert.Equal(
            [(480, 720), (750, 1020)],
            TimeUtil.SubtractIntervals([(480, 1020)], [(720, 750)]));
        Assert.Equal(
            [(500, 1000)],
            TimeUtil.SubtractIntervals([(480, 1020)], [(400, 500), (1000, 1100)]));
        Assert.Empty(TimeUtil.SubtractIntervals([(480, 600)], [(480, 600)]));
        Assert.Equal(240, TimeUtil.TotalCovered([(480, 600), (600, 720)]));
    }
}
