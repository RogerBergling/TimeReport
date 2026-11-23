using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Services;
using TimeReport.Web.Auth;
using TimeReport.Web.Localization;
using TimeReport.Web.Security;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Account;

[Authorize]
public sealed class SettingsModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;

    public SettingsModel(AuthService auth, AuthCookieService cookies)
    {
        _auth = auth;
        _cookies = cookies;
    }

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public string? Language { get; set; }

    [BindProperty]
    public string? TimeZoneId { get; set; }

    [BindProperty]
    public decimal? OvertimeDailyHours { get; set; }

    [BindProperty]
    public decimal? OvertimeWeeklyHours { get; set; }

    [BindProperty]
    public int BackupRetentionCount { get; set; } = 20;

    public IReadOnlyList<TimeZoneOption> TimeZones { get; private set; } = [];

    public IReadOnlyList<LanguageOption> Languages => LocalizationService.Languages;

    public void OnGet()
    {
        DisplayName = CurrentUser.DisplayName;
        Language = CurrentUser.Language;
        TimeZoneId = CurrentUser.TimeZoneId;
        OvertimeDailyHours = CurrentUser.OvertimeDailyMinutes / 60m;
        OvertimeWeeklyHours = CurrentUser.OvertimeWeeklyMinutes / 60m;
        BackupRetentionCount = CurrentUser.BackupRetentionCount;
        TimeZones = TimeZoneService.GetOptions();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TimeZones = TimeZoneService.GetOptions();
        var daily = OvertimeDailyHours ?? 0;
        var weekly = OvertimeWeeklyHours ?? 0;
        if (daily is < 0 or > 24)
        {
            ErrorMessage = WebText.T("The daily overtime threshold must be between 0 and 24 hours.");
            return Page();
        }
        if (weekly is < 0 or > 168)
        {
            ErrorMessage = WebText.T("The weekly overtime threshold must be between 0 and 168 hours.");
            return Page();
        }
        if (BackupRetentionCount is < 1 or > 365)
        {
            ErrorMessage = WebText.T("Backup retention must be between 1 and 365.");
            return Page();
        }

        try
        {
            var updated = _auth.UpdatePreferences(
                CurrentUser,
                DisplayName,
                Language,
                CurrentUser.Theme,
                TimeZoneId,
                (int)Math.Round(daily * 60, MidpointRounding.AwayFromZero),
                (int)Math.Round(weekly * 60, MidpointRounding.AwayFromZero),
                BackupRetentionCount);
            await _cookies.SignInAsync(HttpContext, updated);
            SetStatus(WebText.T("Settings saved."));
            return RedirectToPage();
        }
        catch (AuthException exception)
        {
            ErrorMessage = exception.Message;
            return Page();
        }
    }
}
