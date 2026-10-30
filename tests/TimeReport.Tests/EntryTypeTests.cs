using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class EntryTypeTests
{
    [Fact]
    public void DefaultTypesAreSeeded()
    {
        using var context = new TestContext();
        var types = context.Service.ListEntryTypes();

        Assert.Equal(
            ["Away", "Lunch", "Work", "X-Pause"],
            types.Select(type => type.Name));
        Assert.Equal(4, types.Count);
    }

    [Fact]
    public void CustomWorkTypeIsStoredAndDisplayed()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var meeting = context.Service.CreateEntryType("Meeting", EntryKind.Work);

        var entry = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "09:00",
            project.Id,
            typeId: meeting.Id));

        Assert.Equal(meeting.Id, entry.TypeId);
        Assert.Equal("Meeting", entry.TypeName);
        Assert.Equal(EntryKind.Work, entry.Kind);
        Assert.Equal(60, context.Service.DailyTotals(TestData.Monday).WorkedMinutes);
    }

    [Fact]
    public void CustomAwayTypeUsesTypeNameAsReason()
    {
        using var context = new TestContext();
        var sick = context.Service.CreateEntryType("Sick", EntryKind.Away);

        var entry = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Away,
            "09:00",
            "10:00",
            typeId: sick.Id));

        Assert.Equal("Sick", entry.AwayReason);
        Assert.Equal("Sick", entry.TypeName);
        Assert.Equal(60, context.Service.DailyTotals(TestData.Monday).AwayMinutes);
    }

    [Fact]
    public void CustomLunchTypeIsDeducted()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var lunchType = context.Service.CreateEntryType("Late lunch", EntryKind.Lunch);
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "17:00",
            project.Id));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Lunch,
            "13:00",
            "13:30",
            typeId: lunchType.Id));

        var totals = context.Service.DailyTotals(TestData.Monday);
        Assert.Equal(510, totals.WorkedMinutes);
        Assert.Equal(30, totals.LunchMinutes);
    }

    [Fact]
    public void ArchivedTypeCannotBeUsedForNewEntries()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var type = context.Service.CreateEntryType("Training", EntryKind.Work);
        var existing = context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "09:00",
            project.Id,
            typeId: type.Id));

        context.Service.SetEntryTypeArchived(type.Id, true);

        var exception = Assert.Throws<ValidationException>(() =>
            context.Service.SaveEntry(TestData.Entry(
                TestData.Tuesday,
                EntryKind.Work,
                "08:00",
                "09:00",
                project.Id,
                typeId: type.Id)));
        Assert.Contains("archived", exception.Message);

        context.Service.SaveEntry(existing with
        {
            EndMinute = TimeUtil.ParseTime("09:30"),
        });
    }

    [Fact]
    public void UsedTypeCannotBeDeletedButUnusedTypeCan()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var used = context.Service.CreateEntryType("Meeting", EntryKind.Work);
        var unused = context.Service.CreateEntryType("Unused", EntryKind.Work);
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday,
            EntryKind.Work,
            "08:00",
            "09:00",
            project.Id,
            typeId: used.Id));

        Assert.Equal(1, context.Service.EntryTypeUsageCount(used.Id));
        Assert.Throws<ValidationException>(() => context.Service.DeleteEntryType(used.Id));
        context.Service.DeleteEntryType(unused.Id);
        Assert.DoesNotContain(
            context.Service.ListEntryTypes(includeArchived: true),
            type => type.Id == unused.Id);
    }

    [Fact]
    public void TypeNamesAreTrimmedAndUnique()
    {
        using var context = new TestContext();
        var type = context.Service.CreateEntryType("  Client   meeting  ", EntryKind.Work);
        Assert.Equal("Client meeting", type.Name);
        Assert.Throws<ValidationException>(() =>
            context.Service.CreateEntryType("client meeting", EntryKind.Work));
    }
}
