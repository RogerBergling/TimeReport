using System.Globalization;
using TimeReport.Models;

namespace TimeReport.Services;

public sealed class ExcelExportService
{
    private readonly TimeReportService _service;

    public ExcelExportService(TimeReportService service)
    {
        _service = service;
    }

    public Task<string> ExportAsync(
        DateOnly start,
        DateOnly end,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
        {
            throw new ValidationException(LocalizationService.T("ExportInvalidRange"));
        }

        var entries = _service.ListEntries(start, end);
        var totals = _service.RangeTotals(start, end);
        var dailyTotals = _service.TotalsByDay(start, end);
        var monthlyTotals = BuildMonthlyTotals(start, end);
        var projects = _service.ListProjects(includeArchived: true);
        var types = _service.ListEntryTypes(includeArchived: true);

        var sheets = new List<XlsxSheet>
        {
            BuildSummarySheet(start, end, entries, totals, projects.Count, types.Count, _service.Database.Path),
            BuildEntriesSheet(entries),
            BuildDailySheet(dailyTotals),
            BuildMonthlySheet(monthlyTotals),
            BuildProjectsSheet(projects, entries),
            BuildTypesSheet(types, entries),
        };

        return Task.Run(
            () =>
            {
                XlsxWriter.Write(path, sheets);
                return Path.GetFullPath(path);
            },
            cancellationToken);
    }

    private static XlsxSheet BuildSummarySheet(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<Entry> entries,
        Totals totals,
        int projectCount,
        int typeCount,
        string databasePath)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[] { Header("ExportTitle"), Text("TimeReport") },
            new[] { Text(LocalizationService.T("ExportGenerated")), Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)) },
            new[] { Text(LocalizationService.T("ExportPeriod")), Text($"{start:yyyy-MM-dd} – {end:yyyy-MM-dd}") },
            new[] { Text(LocalizationService.T("ExportDays")), Number((end.DayNumber - start.DayNumber) + 1) },
            new[] { Text(LocalizationService.T("ExportEntries")), Number(entries.Count) },
            Array.Empty<XlsxCell>(),
            new[] { Text(LocalizationService.T("LabelWorked")), Duration(totals.WorkedMinutes) },
            new[] { Text(LocalizationService.T("LabelLunch")), Duration(totals.LunchMinutes) },
            new[] { Text(LocalizationService.T("LabelAway")), Duration(totals.AwayMinutes) },
            new[] { Text(LocalizationService.T("LabelXPause")), Duration(totals.XPauseMinutes) },
            new[] { Text(LocalizationService.T("LabelDaysWorked")), Number(totals.DaysWorked) },
            new[] { Text(LocalizationService.T("LabelDaysAway")), Number(totals.DaysAway) },
            Array.Empty<XlsxCell>(),
            new[] { Text(LocalizationService.T("ExportDatabase")), Text(databasePath) },
            new[] { Text(LocalizationService.T("ExportProjects")), Number(projectCount) },
            new[] { Text(LocalizationService.T("ExportTypes")), Number(typeCount) },
        };

        return new XlsxSheet(
            LocalizationService.T("ExportSheetSummary"),
            [32, 52],
            rows);
    }

    private static XlsxSheet BuildEntriesSheet(IReadOnlyList<Entry> entries)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                Header("ExportDate"),
                Header("ExportWeekday"),
                Header("ExportType"),
                Header("ExportCategory"),
                Header("ExportProject"),
                Header("ExportAwayReason"),
                Header("ExportStart"),
                Header("ExportEnd"),
                Header("ExportDuration"),
            },
        };

        foreach (var entry in entries)
        {
            rows.Add(new[]
            {
                XlsxCell.DateCell(entry.Date),
                Text(LocalizationService.WeekdayNames[(int)entry.Date.DayOfWeek]),
                Text(entry.TypeName ?? entry.Kind.DisplayName()),
                Text(entry.Kind.DisplayName()),
                Text(entry.ProjectName ?? string.Empty),
                Text(entry.AwayReason ?? string.Empty),
                Text(TimeUtil.FormatTime(entry.StartMinute)),
                Text(TimeUtil.FormatTime(entry.EndMinute)),
                Duration(entry.DurationMinutes),
            });
        }

        return new XlsxSheet(
            LocalizationService.T("ExportSheetEntries"),
            [12, 12, 20, 14, 30, 30, 10, 10, 12],
            rows);
    }

    private static XlsxSheet BuildDailySheet(
        IReadOnlyList<(DateOnly Date, Totals Totals)> dailyTotals)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                Header("ExportDate"),
                Header("ExportWeekday"),
                Header("LabelWorked"),
                Header("LabelLunch"),
                Header("LabelAway"),
                Header("LabelXPause"),
            },
        };

        foreach (var (day, totals) in dailyTotals)
        {
            rows.Add(new[]
            {
                XlsxCell.DateCell(day),
                Text(LocalizationService.WeekdayNames[(int)day.DayOfWeek]),
                Duration(totals.WorkedMinutes),
                Duration(totals.LunchMinutes),
                Duration(totals.AwayMinutes),
                Duration(totals.XPauseMinutes),
            });
        }

        return new XlsxSheet(
            LocalizationService.T("ExportSheetDaily"),
            [12, 12, 12, 12, 12, 12],
            rows);
    }

    private static XlsxSheet BuildMonthlySheet(
        IReadOnlyList<(DateOnly Month, Totals Totals)> monthlyTotals)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                Header("ExportMonth"),
                Header("LabelWorked"),
                Header("LabelLunch"),
                Header("LabelAway"),
                Header("LabelXPause"),
                Header("LabelDaysWorked"),
                Header("LabelDaysAway"),
            },
        };

        foreach (var (month, totals) in monthlyTotals)
        {
            rows.Add(new[]
            {
                Text($"{TimeUtil.FormatMonthName(month)} {month.Year}"),
                Duration(totals.WorkedMinutes),
                Duration(totals.LunchMinutes),
                Duration(totals.AwayMinutes),
                Duration(totals.XPauseMinutes),
                Number(totals.DaysWorked),
                Number(totals.DaysAway),
            });
        }

        return new XlsxSheet(
            LocalizationService.T("ExportSheetMonthly"),
            [20, 12, 12, 12, 12, 12, 12],
            rows);
    }

    private static XlsxSheet BuildProjectsSheet(
        IReadOnlyList<Project> projects,
        IReadOnlyList<Entry> entries)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                Header("ExportProject"),
                Header("ExportStatus"),
                Header("ExportEntries"),
                Header("ExportWorkHours"),
            },
        };

        foreach (var project in projects)
        {
            var projectEntries = entries.Where(entry => entry.ProjectId == project.Id).ToList();
            var workMinutes = projectEntries
                .Where(entry => entry.Kind == EntryKind.Work)
                .Sum(entry => entry.DurationMinutes);
            rows.Add(new[]
            {
                Text(project.Name),
                Text(LocalizationService.T(project.Archived ? "ProjectStatusArchived" : "ProjectStatusActive")),
                Number(projectEntries.Count),
                Duration(workMinutes),
            });
        }

        return new XlsxSheet(
            LocalizationService.T("ExportSheetProjects"),
            [32, 12, 10, 14],
            rows);
    }

    private static XlsxSheet BuildTypesSheet(
        IReadOnlyList<EntryType> types,
        IReadOnlyList<Entry> entries)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                Header("ExportType"),
                Header("ExportCategory"),
                Header("ExportStatus"),
                Header("ExportEntries"),
                Header("ExportTotalHours"),
            },
        };

        foreach (var type in types)
        {
            var typeEntries = entries.Where(entry => entry.TypeId == type.Id).ToList();
            rows.Add(new[]
            {
                Text(type.Name),
                Text(type.Category.DisplayName()),
                Text(LocalizationService.T(type.Archived ? "ProjectStatusArchived" : "ProjectStatusActive")),
                Number(typeEntries.Count),
                Duration(typeEntries.Sum(entry => entry.DurationMinutes)),
            });
        }

        return new XlsxSheet(
            LocalizationService.T("ExportSheetTypes"),
            [32, 16, 12, 10, 14],
            rows);
    }

    private List<(DateOnly Month, Totals Totals)> BuildMonthlyTotals(DateOnly start, DateOnly end)
    {
        var result = new List<(DateOnly Month, Totals Totals)>();
        var month = new DateOnly(start.Year, start.Month, 1);
        var lastMonth = new DateOnly(end.Year, end.Month, 1);
        while (month <= lastMonth)
        {
            var (monthStart, monthEnd) = TimeUtil.MonthBounds(month);
            var rangeStart = monthStart < start ? start : monthStart;
            var rangeEnd = monthEnd > end ? end : monthEnd;
            result.Add((monthStart, _service.RangeTotals(rangeStart, rangeEnd)));
            month = month.AddMonths(1);
        }
        return result;
    }

    private static XlsxCell Header(string key) => XlsxCell.HeaderCell(LocalizationService.T(key));

    private static XlsxCell Text(string value) => XlsxCell.TextCell(value);

    private static XlsxCell Number(int value) => XlsxCell.NumberCell(value);

    private static XlsxCell Duration(int minutes) => XlsxCell.DurationCell(minutes);
}
