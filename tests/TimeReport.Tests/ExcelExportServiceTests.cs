using System.IO.Compression;
using System.Xml.Linq;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class ExcelExportServiceTests
{
    [Fact]
    public async Task ExportCreatesExpectedWorkbookAndSheets()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        var meeting = context.Service.CreateEntryType("Meeting", EntryKind.Work);
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "17:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Lunch, "12:00", "12:30"));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.XPause, "10:00", "10:15"));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Tuesday, EntryKind.Away, "09:00", "12:00", reason: "Sick"));
        context.Service.SaveEntry(TestData.Entry(
            TestData.Tuesday, EntryKind.Work, "13:00", "17:00", project.Id, typeId: meeting.Id));

        var path = Path.Combine(context.Directory, "export.xlsx");
        var exporter = new ExcelExportService(context.Service);
        var saved = await exporter.ExportAsync(
            TestData.Monday,
            TestData.Monday.AddDays(6),
            path);

        Assert.Equal(Path.GetFullPath(path), saved);
        Assert.True(File.Exists(path));

        using var archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        Assert.NotNull(archive.GetEntry("xl/styles.xml"));
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet6.xml"));

        var workbook = LoadXml(archive, "xl/workbook.xml");
        var sheetNames = workbook.Descendants()
            .Where(element => element.Name.LocalName == "sheet")
            .Select(element => (string?)element.Attribute("name"))
            .ToList();
        Assert.Equal(
            ["Summary", "Entries", "Daily totals", "Monthly totals", "Projects", "Types"],
            sheetNames);

        var entriesSheet = LoadXml(archive, "xl/worksheets/sheet2.xml");
        Assert.Equal(6, RowCount(entriesSheet)); // header + 5 entries
        Assert.Contains("Acme", AllText(entriesSheet));
        Assert.Contains("Meeting", AllText(entriesSheet));
        Assert.Contains("Sick", AllText(entriesSheet));

        var dailySheet = LoadXml(archive, "xl/worksheets/sheet3.xml");
        Assert.Equal(8, RowCount(dailySheet)); // header + 7 days

        var monthlySheet = LoadXml(archive, "xl/worksheets/sheet4.xml");
        Assert.Equal(3, RowCount(monthlySheet)); // header + October and November

        var projectsSheet = LoadXml(archive, "xl/worksheets/sheet5.xml");
        Assert.Equal(2, RowCount(projectsSheet));

        var typesSheet = LoadXml(archive, "xl/worksheets/sheet6.xml");
        Assert.Equal(6, RowCount(typesSheet)); // header + 4 defaults + Meeting
    }

    [Fact]
    public async Task ExportOnlyIncludesSelectedRange()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            TestData.Monday, EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(
            new DateOnly(2026, 11, 15), EntryKind.Work, "08:00", "12:00", project.Id));

        var path = Path.Combine(context.Directory, "october.xlsx");
        var exporter = new ExcelExportService(context.Service);
        await exporter.ExportAsync(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            path);

        using var archive = ZipFile.OpenRead(path);
        var entriesSheet = LoadXml(archive, "xl/worksheets/sheet2.xml");
        Assert.Equal(2, RowCount(entriesSheet)); // header + one October entry
        Assert.DoesNotContain("2026-11-15", AllText(entriesSheet));
    }

    [Fact]
    public async Task MonthlyTotalsAreClippedToSelectedRange()
    {
        using var context = new TestContext();
        var project = context.Service.CreateProject("Acme");
        context.Service.SaveEntry(TestData.Entry(
            new DateOnly(2026, 10, 1), EntryKind.Work, "08:00", "12:00", project.Id));
        context.Service.SaveEntry(TestData.Entry(
            new DateOnly(2026, 10, 20), EntryKind.Work, "08:00", "12:00", project.Id));

        var path = Path.Combine(context.Directory, "partial-october.xlsx");
        var exporter = new ExcelExportService(context.Service);
        await exporter.ExportAsync(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 31),
            path);

        using var archive = ZipFile.OpenRead(path);
        var monthlySheet = LoadXml(archive, "xl/worksheets/sheet4.xml");
        Assert.Equal(2, RowCount(monthlySheet)); // header + October only
        Assert.Equal(4d / 24, CellNumber(monthlySheet, "B2"), 6);
    }

    [Fact]
    public async Task InvalidRangeIsRejected()
    {
        using var context = new TestContext();
        var exporter = new ExcelExportService(context.Service);

        await Assert.ThrowsAsync<ValidationException>(() =>
            exporter.ExportAsync(
                new DateOnly(2026, 10, 31),
                new DateOnly(2026, 10, 1),
                Path.Combine(context.Directory, "invalid.xlsx")));
    }

    private static XDocument LoadXml(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing {name}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static int RowCount(XDocument sheet) =>
        sheet.Descendants().Count(element => element.Name.LocalName == "row");

    private static double CellNumber(XDocument sheet, string reference)
    {
        var cell = sheet.Descendants()
            .Single(element => element.Name.LocalName == "c" &&
                               (string?)element.Attribute("r") == reference);
        var value = cell.Elements().Single(element => element.Name.LocalName == "v");
        return double.Parse(value.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string AllText(XDocument sheet) =>
        string.Join(" ", sheet.Descendants()
            .Where(element => element.Name.LocalName == "t")
            .Select(element => element.Value));
}
