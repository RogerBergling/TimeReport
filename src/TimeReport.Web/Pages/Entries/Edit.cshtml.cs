using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Entries;

[Authorize]
public sealed class EditModel : AppPageModel
{
    private readonly UserDataStore _data;

    public EditModel(UserDataStore data)
    {
        _data = data;
    }

    [BindProperty]
    public long? Id { get; set; }

    [BindProperty]
    public string? Date { get; set; }

    [BindProperty]
    public long? TypeId { get; set; }

    [BindProperty]
    public long? ProjectId { get; set; }

    [BindProperty]
    public string? AwayReason { get; set; }

    [BindProperty]
    public string? Start { get; set; }

    [BindProperty]
    public string? End { get; set; }

    [BindProperty]
    public string? TimeZoneId { get; set; }

    public IReadOnlyList<EntryType> Types { get; private set; } = [];

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public IReadOnlyList<TimeZoneOption> TimeZones { get; private set; } = [];

    public bool IsEdit => Id is not null;

    public string? XPauseHint { get; private set; }

    public string ReturnWeek { get; private set; } = string.Empty;

    public IActionResult OnGet(long? id = null, string? date = null)
    {
        var service = _data.Current.Service;
        if (id is long entryId)
        {
            var entry = service.GetEntry(entryId);
            if (entry is null)
            {
                SetError("That entry no longer exists.");
                return RedirectToPage("/Index");
            }

            Id = entry.Id;
            Date = entry.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            TypeId = entry.TypeId;
            ProjectId = entry.ProjectId;
            AwayReason = entry.AwayReason;
            Start = TimeUtil.FormatTime(entry.StartMinute);
            End = TimeUtil.FormatTime(entry.EndMinute);
            TimeZoneId = TimeZoneService.IsValid(entry.TimeZoneId)
                ? TimeZoneService.ResolveId(entry.TimeZoneId)
                : service.TimeZoneId;
            ReturnWeek = TimeUtil.WeekStart(entry.Date)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            var day = service.Today;
            if (!string.IsNullOrWhiteSpace(date) &&
                DateOnly.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                day = parsed;
            }

            var types = service.ListEntryTypes();
            EntryType? defaultType = null;
            foreach (var type in types)
            {
                defaultType ??= type;
                if (type.Category == EntryKind.Work)
                {
                    defaultType = type;
                    break;
                }
            }
            TypeId = defaultType?.Id;
            Date = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            TimeZoneId = service.TimeZoneId;
            var now = TimeZoneService.Now(service.TimeZoneId);
            var start = Math.Min(23 * 60, (now.Hour * 60 + now.Minute) / 15 * 15);
            Start = TimeUtil.FormatTime(start);
            End = TimeUtil.FormatTime(Math.Min(TimeUtil.MinutesPerDay, start + 60));
            ReturnWeek = TimeUtil.WeekStart(day)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        Reload(service);
        return Page();
    }

    public IActionResult OnPost()
    {
        var service = _data.Current.Service;
        try
        {
            var day = TimeUtil.ParseDate(Date ?? string.Empty);
            var type = TypeId is long typeId
                ? service.GetEntryType(typeId) ?? throw new ValidationException(
                    LocalizationService.T("ValidationTypeMissing"))
                : throw new ValidationException(LocalizationService.T("ValidationTypeMissing"));
            var start = TimeUtil.ParseTime(Start ?? string.Empty);
            var end = TimeUtil.ParseTime(End ?? string.Empty, allowEndOfDay: true);
            var timeZone = TimeZoneService.IsValid(TimeZoneId)
                ? TimeZoneService.ResolveId(TimeZoneId)
                : service.TimeZoneId;
            var entry = new Entry(
                Id,
                day,
                type.Category,
                start,
                end,
                ProjectId,
                null,
                string.IsNullOrWhiteSpace(AwayReason) ? null : AwayReason.Trim(),
                type.Id,
                type.Name,
                timeZone);
            var saved = service.SaveEntry(entry);
            SetStatus(IsEdit ? "Entry updated." : "Entry added.");
            return RedirectToPage(
                "/Index",
                new
                {
                    week = TimeUtil.WeekStart(saved.Date)
                        .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                });
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (DatabaseException exception)
        {
            ErrorMessage = exception.Message;
        }

        Reload(service);
        return Page();
    }

    public IActionResult OnPostDelete(long id)
    {
        var service = _data.Current.Service;
        try
        {
            var entry = service.GetEntry(id);
            service.DeleteEntry(id);
            SetStatus("Entry deleted.");
            return RedirectToPage(
                "/Index",
                new
                {
                    week = entry is null
                        ? null
                        : TimeUtil.WeekStart(entry.Date)
                            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                });
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (Exception)
        {
            SetError("The entry could not be deleted.");
        }

        return RedirectToPage("/Index");
    }

    private void Reload(TimeReportService service)
    {
        Types = service.ListEntryTypes(includeArchived: IsEdit);
        Projects = service.ListProjects(includeArchived: IsEdit);
        TimeZones = TimeZoneService.GetOptions();
        if (!string.IsNullOrWhiteSpace(Date) &&
            DateOnly.TryParseExact(
                Date,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var day))
        {
            ReturnWeek = TimeUtil.WeekStart(day)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                var used = service.XPauseUsedInWeek(TimeUtil.WeekStart(day), Id);
                var remaining = Math.Max(0, TimeUtil.XPauseWeeklyLimit - used);
                XPauseHint = LocalizationService.T(
                    "EntryDialogXPauseHint",
                    TimeUtil.FormatDuration(used),
                    TimeUtil.FormatDuration(remaining),
                    day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                        ? LocalizationService.T("EntryDialogXPauseWeekdayNote")
                        : string.Empty);
            }
            catch (ValidationException)
            {
                XPauseHint = null;
            }
        }
    }
}
