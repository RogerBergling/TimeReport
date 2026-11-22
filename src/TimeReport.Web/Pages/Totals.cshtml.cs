using Microsoft.AspNetCore.Authorization;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[Authorize]
public sealed class TotalsModel : AppPageModel
{
    private readonly UserDataStore _data;

    public TotalsModel(UserDataStore data)
    {
        _data = data;
    }

    public string PeriodCode { get; private set; } = "week";

    public string PeriodLabel { get; private set; } = string.Empty;

    public Totals Total { get; private set; } = new();

    public IReadOnlyList<TotalsRowView> Rows { get; private set; } = [];

    public bool CanNavigate { get; private set; } = true;

    public string Anchor { get; private set; } = string.Empty;

    public string PreviousAnchor { get; private set; } = string.Empty;

    public string NextAnchor { get; private set; } = string.Empty;

    public IReadOnlyList<PeriodOptionView> PeriodOptions { get; } =
    [
        new("week", LocalizationService.T("PeriodWeek")),
        new("month", LocalizationService.T("PeriodMonth")),
        new("year", LocalizationService.T("PeriodYear")),
        new("all", LocalizationService.T("PeriodAllTime")),
    ];

    public void OnGet(string? period = null, string? anchor = null)
    {
        var service = _data.Current.Service;
        PeriodCode = period is "week" or "month" or "year" or "all" ? period : "week";
        var day = service.Today;
        if (!string.IsNullOrWhiteSpace(anchor) &&
            DateOnly.TryParseExact(
                anchor,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            day = parsed;
        }

        Anchor = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var rows = new List<TotalsRowView>();
        switch (PeriodCode)
        {
            case "month":
                BuildMonth(service, day, rows);
                break;
            case "year":
                BuildYear(service, day, rows);
                break;
            case "all":
                BuildAll(service, rows);
                break;
            default:
                BuildWeek(service, day, rows);
                break;
        }
        Rows = rows;
    }

    private void BuildWeek(TimeReportService service, DateOnly day, List<TotalsRowView> rows)
    {
        CanNavigate = true;
        var start = TimeUtil.WeekStart(day);
        var end = start.AddDays(6);
        Anchor = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PreviousAnchor = start.AddDays(-7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        NextAnchor = start.AddDays(7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PeriodLabel =
            $"{LocalizationService.T("LabelWeekPrefix")} {TimeUtil.WeekNumber(start)} · " +
            TimeUtil.FormatWeekRange(start, end);
        var today = service.Today;
        foreach (var (date, totals) in service.TotalsByDay(start, end))
        {
            var label = TimeUtil.FormatDayShort(date);
            if (date == today)
            {
                label += $" · {LocalizationService.T("LabelToday")}";
            }
            rows.Add(new TotalsRowView(label, totals));
        }
        Total = service.RangeTotals(start, end);
    }

    private void BuildMonth(TimeReportService service, DateOnly day, List<TotalsRowView> rows)
    {
        CanNavigate = true;
        var (start, end) = TimeUtil.MonthBounds(day);
        Anchor = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PreviousAnchor = start.AddMonths(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        NextAnchor = start.AddMonths(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PeriodLabel = $"{TimeUtil.FormatMonthName(start)} {start.Year}";
        var today = service.Today;
        foreach (var (date, totals) in service.TotalsByDay(start, end))
        {
            var label = TimeUtil.FormatDayShort(date);
            if (date == today)
            {
                label += $" · {LocalizationService.T("LabelToday")}";
            }
            rows.Add(new TotalsRowView(label, totals));
        }
        Total = service.RangeTotals(start, end);
    }

    private void BuildYear(TimeReportService service, DateOnly day, List<TotalsRowView> rows)
    {
        CanNavigate = true;
        var (start, end) = TimeUtil.YearBounds(day);
        Anchor = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PreviousAnchor = new DateOnly(start.Year - 1, 1, 1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        NextAnchor = new DateOnly(start.Year + 1, 1, 1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        PeriodLabel = start.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var (month, totals) in service.TotalsByMonth(start.Year))
        {
            rows.Add(new TotalsRowView(TimeUtil.FormatMonthName(month), totals));
        }
        Total = service.RangeTotals(start, end);
    }

    private void BuildAll(TimeReportService service, List<TotalsRowView> rows)
    {
        CanNavigate = false;
        var bounds = service.AllTimeBounds();
        if (bounds is null)
        {
            PeriodLabel = LocalizationService.T("PeriodAllTime");
            Total = new Totals();
            return;
        }

        var (start, end) = bounds.Value;
        PeriodLabel = start.Year == end.Year
            ? $"{LocalizationService.T("PeriodAllTime")} · {start.Year}"
            : $"{LocalizationService.T("PeriodAllTime")} · {start.Year}–{end.Year}";
        foreach (var (year, totals) in service.TotalsByYear(start.Year, end.Year))
        {
            rows.Add(new TotalsRowView(
                year.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                totals));
        }
        Total = service.RangeTotals(start, end);
        PreviousAnchor = Anchor;
        NextAnchor = Anchor;
    }
}
