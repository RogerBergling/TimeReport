using Microsoft.AspNetCore.Authorization;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[Authorize]
public sealed class ExportModel : AppPageModel
{
    private readonly UserDataStore _data;

    public ExportModel(UserDataStore data)
    {
        _data = data;
    }

    public string Start { get; private set; } = string.Empty;

    public string End { get; private set; } = string.Empty;

    public string ThisMonthStart { get; private set; } = string.Empty;

    public string ThisMonthEnd { get; private set; } = string.Empty;

    public string LastMonthStart { get; private set; } = string.Empty;

    public string LastMonthEnd { get; private set; } = string.Empty;

    public string ThisYearStart { get; private set; } = string.Empty;

    public string ThisYearEnd { get; private set; } = string.Empty;

    public string LastYearStart { get; private set; } = string.Empty;

    public string LastYearEnd { get; private set; } = string.Empty;

    public string? AllTimeStart { get; private set; }

    public string? AllTimeEnd { get; private set; }

    public void OnGet()
    {
        var service = _data.Current.Service;
        var today = service.Today;
        var (thisMonthStart, thisMonthEnd) = TimeUtil.MonthBounds(today);
        var lastMonth = today.AddMonths(-1);
        var (lastMonthStart, lastMonthEnd) = TimeUtil.MonthBounds(lastMonth);
        var (thisYearStart, thisYearEnd) = TimeUtil.YearBounds(today);
        var (lastYearStart, lastYearEnd) = TimeUtil.YearBounds(today.AddYears(-1));
        var allTime = service.AllTimeBounds();

        Start = Format(thisMonthStart);
        End = Format(thisMonthEnd);
        ThisMonthStart = Format(thisMonthStart);
        ThisMonthEnd = Format(thisMonthEnd);
        LastMonthStart = Format(lastMonthStart);
        LastMonthEnd = Format(lastMonthEnd);
        ThisYearStart = Format(thisYearStart);
        ThisYearEnd = Format(thisYearEnd);
        LastYearStart = Format(lastYearStart);
        LastYearEnd = Format(lastYearEnd);
        AllTimeStart = allTime is null ? null : Format(allTime.Value.Start);
        AllTimeEnd = allTime is null ? null : Format(allTime.Value.End);
    }

    private static string Format(DateOnly value) =>
        value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
