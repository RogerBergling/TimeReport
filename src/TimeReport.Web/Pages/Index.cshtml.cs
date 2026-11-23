using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Services;
using TimeReport.Web.Localization;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[Authorize]
public sealed class IndexModel : AppPageModel
{
    private readonly UserDataStore _data;

    public IndexModel(UserDataStore data)
    {
        _data = data;
    }

    public TimesheetWeekView Week { get; private set; } = null!;

    public void OnGet(string? week = null)
    {
        Week = BuildWeek(week);
    }

    public IActionResult OnPostDelete(long id, string? week)
    {
        try
        {
            _data.Current.Service.DeleteEntry(id);
            SetStatus(WebText.T("Entry deleted."));
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (Exception)
        {
            SetError(WebText.T("The entry could not be deleted."));
        }

        return RedirectToPage(new { week });
    }

    private TimesheetWeekView BuildWeek(string? value)
    {
        var service = _data.Current.Service;
        var anchor = service.Today;
        if (!string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            anchor = parsed;
        }

        var start = TimeUtil.WeekStart(anchor);
        var end = start.AddDays(6);
        var entries = service.ListEntries(start, end)
            .GroupBy(entry => entry.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var today = service.Today;
        var days = new List<TimesheetDayView>();
        foreach (var day in TimeUtil.DateRangeDays(start, end))
        {
            var dayEntries = entries.TryGetValue(day, out var list) ? list : [];
            days.Add(new TimesheetDayView(
                day,
                day == today,
                dayEntries.Select(entry => new EntryRowView(entry)).ToList(),
                service.DailyTotals(day, dayEntries)));
        }

        var label =
            $"{LocalizationService.T("LabelWeekPrefix")} {TimeUtil.WeekNumber(start)} · " +
            TimeUtil.FormatWeekRange(start, end);
        return new TimesheetWeekView(start, end, label, days, service.RangeTotals(start, end));
    }
}
