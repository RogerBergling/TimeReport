using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class ValidationTests
{
    private static Entry Work(
        TimeReportService service,
        Project project,
        DateOnly? day = null,
        string start = "08:00",
        string end = "17:00") =>
        service.SaveEntry(TestData.Entry(
            day ?? TestData.Monday,
            EntryKind.Work,
            start,
            end,
            project.Id));

    [Fact]
    public void WorkRequiresProject()
    {
        using var context = new TestContext();
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Work, "08:00", "16:00")));
        Assert.Contains("Choose a project", exception.Message);
    }

    [Fact]
    public void EndMustBeAfterStart()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Work, "16:00", "08:00", project.Id)));
    }

    [Fact]
    public void WorkCannotOverlapWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, start: "08:00", end: "12:00");
        var exception = Assert.Throws<ValidationException>(() =>
            Work(context.Service, project, start: "11:00", end: "13:00"));
        Assert.Contains("overlaps work", exception.Message);
    }

    [Fact]
    public void WorkCannotOverlapAway()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Away, "09:00", "10:00", reason: "Sick"));
        var exception = Assert.Throws<ValidationException>(() =>
            Work(context.Service, project, start: "08:00", end: "12:00"));
        Assert.Contains("overlaps away", exception.Message);
    }

    [Fact]
    public void LunchMayOverlapWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, start: "08:00", end: "17:00");
        var lunch = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));
        Assert.Equal(30, lunch.DurationMinutes);
    }

    [Fact]
    public void LunchCannotOverlapLunch()
    {
        using var context = new TestContext();
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Lunch, "12:15", "12:45")));
        Assert.Contains("overlaps lunch", exception.Message);
    }

    [Fact]
    public void AwayReasonDefaultsToTypeName()
    {
        using var context = new TestContext();
        var away = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Away, "09:00", "10:00"));
        Assert.Equal("Away", away.AwayReason);
        Assert.Equal("Away", away.TypeName);
    }

    [Fact]
    public void AwayCannotOverlapWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, start: "08:00", end: "17:00");
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Away, "09:00", "10:00", reason: "Sick")));
        Assert.Contains("overlaps work", exception.Message);
    }

    [Fact]
    public void AwayCannotOverlapLunch()
    {
        using var context = new TestContext();
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Away, "12:15", "13:00", reason: "Sick")));
        Assert.Contains("overlaps lunch", exception.Message);
    }

    [Fact]
    public void AwayReasonIsTrimmed()
    {
        using var context = new TestContext();
        var away = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Away, "09:00", "10:00", reason: "  Sick  "));
        Assert.Equal("Sick", away.AwayReason);
    }

    [Fact]
    public void NonWorkEntriesCannotHaveProject()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Lunch, "12:00", "12:30", project.Id)));
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.Away, "12:00", "13:00", project.Id, "Sick")));
    }

    [Fact]
    public void XPauseOnlyOnWeekdays()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, TestData.Saturday, "08:00", "17:00");
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Saturday, EntryKind.XPause, "10:00", "10:15")));
        Assert.Contains("Monday to Friday", exception.Message);
    }

    [Fact]
    public void XPauseRequires15MinuteSteps()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project);
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "10:00", "10:10")));
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "10:00", "10:20")));
    }

    [Fact]
    public void XPauseMustBeInsideWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project);
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "07:45", "08:00")));
        Assert.Contains("inside a work entry", exception.Message);
    }

    [Fact]
    public void XPauseCannotStartOrEndTheDay()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project);
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "08:00", "08:15")));
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "16:45", "17:00")));
    }

    [Fact]
    public void XPauseCannotSpanAWorkGap()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, start: "08:00", end: "12:00");
        Work(context.Service, project, start: "13:00", end: "17:00");
        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "11:45", "12:15")));
    }

    [Fact]
    public void XPauseCannotOverlapLunch()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project);
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.Lunch, "10:00", "10:30"));
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Monday, EntryKind.XPause, "10:15", "10:30")));
        Assert.Contains("overlaps lunch", exception.Message);
    }

    [Fact]
    public void XPauseWeeklyLimit()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, TestData.Monday);
        Work(context.Service, project, TestData.Tuesday);
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:30"));
        context.Service.SaveEntry(TestData.Entry(TestData.Tuesday, EntryKind.XPause, "10:00", "10:30"));
        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Tuesday, EntryKind.XPause, "11:00", "11:15")));
        Assert.Contains("1:00 per week", exception.Message);
    }

    [Fact]
    public void EditingXPauseUsesUpdatedDurationForWeeklyLimit()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project, TestData.Monday);
        Work(context.Service, project, TestData.Tuesday);
        var monday = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.XPause, "10:00", "10:30"));
        var tuesday = context.Service.SaveEntry(TestData.Entry(
            TestData.Tuesday, EntryKind.XPause, "10:00", "10:15"));

        tuesday = context.Service.SaveEntry(tuesday with
        {
            EndMinute = TimeUtil.ParseTime("10:30"),
        });
        Assert.Equal(30, tuesday.DurationMinutes);

        Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(monday with
            {
                EndMinute = TimeUtil.ParseTime("10:45"),
            }));
    }

    [Fact]
    public void EditingWorkCanInvalidateXPause()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var savedWork = Work(context.Service, project);
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:15"));

        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(savedWork with
            {
                EndMinute = TimeUtil.ParseTime("10:10"),
            }));
        Assert.Contains("no longer inside", exception.Message);
    }

    [Fact]
    public void DeletingWorkWithXPauseIsRejected()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var savedWork = Work(context.Service, project);
        var xpause = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.XPause, "10:00", "10:15"));

        Assert.Throws<ValidationException>(() => context.Service.DeleteEntry(savedWork.Id!.Value));
        context.Service.DeleteEntry(xpause.Id!.Value);
        context.Service.DeleteEntry(savedWork.Id!.Value);
    }

    [Fact]
    public void MovingWorkToAnotherWeekCannotOrphanXPause()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var savedWork = Work(context.Service, project);
        context.Service.SaveEntry(TestData.Entry(TestData.Monday, EntryKind.XPause, "10:00", "10:15"));

        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(savedWork with
            {
                Date = new DateOnly(2026, 11, 2),
            }));
        Assert.Contains("no longer inside", exception.Message);
    }

    [Fact]
    public void ArchivedProjectCannotBeUsedForNewWork()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var savedWork = Work(context.Service, project);
        context.Service.SetProjectArchived(project.Id, true);

        var exception = Assert.Throws<ValidationException>(() =>
            Work(context.Service, project, TestData.Tuesday));
        Assert.Contains("archived", exception.Message);

        context.Service.SaveEntry(savedWork with
        {
            EndMinute = TimeUtil.ParseTime("16:00"),
        });
    }

    [Fact]
    public void SixtyMinutesInOneDayIsAllowed()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        Work(context.Service, project);
        var xpause = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.XPause, "09:00", "10:00"));
        Assert.Equal(60, xpause.DurationMinutes);
    }
}
