using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Localization;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed partial class UsersModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly UserDataStore _data;
    private readonly ILogger<UsersModel> _logger;

    public UsersModel(AuthService auth, UserDataStore data, ILogger<UsersModel> logger)
    {
        _auth = auth;
        _data = data;
        _logger = logger;
    }

    [BindProperty]
    public string? NewUsername { get; set; }

    [BindProperty]
    public string? NewDisplayName { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public bool NewIsAdmin { get; set; }

    public IReadOnlyList<AuthUser> Users { get; private set; } = [];

    public string? TemporaryPassword { get; private set; }

    public string? TemporaryPasswordUser { get; private set; }

    public void OnGet() => Load();

    public IActionResult OnPostCreate()
    {
        try
        {
            var (created, temporary) = _auth.CreateUser(
                CurrentUser,
                NewUsername,
                NewDisplayName,
                NewPassword,
                NewIsAdmin,
                ClientIp);
            if (temporary is not null)
            {
                TemporaryPassword = temporary;
                TemporaryPasswordUser = created.Username;
                Load();
                return Page();
            }

            SetStatus(WebText.T("User created."));
            return RedirectToPage();
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
            NewPassword = null;
            Load();
            return Page();
        }
        catch (AuthDatabaseException exception)
        {
            SetError(exception.Message);
            NewPassword = null;
            Load();
            return Page();
        }
    }

    public IActionResult OnPostResetPassword(string userId)
    {
        var target = _auth.GetUserById(userId);
        if (target is null)
        {
            SetError(WebText.T("The user no longer exists."));
            return RedirectToPage();
        }

        try
        {
            TemporaryPassword = _auth.ResetPassword(CurrentUser, target, ClientIp);
            TemporaryPasswordUser = target.Username;
            Load();
            return Page();
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
            return RedirectToPage();
        }
    }

    public IActionResult OnPostResetMfa(string userId)
    {
        var target = _auth.GetUserById(userId);
        if (target is null)
        {
            SetError(WebText.T("The user no longer exists."));
            return RedirectToPage();
        }

        try
        {
            _auth.ResetTotp(CurrentUser, target, ClientIp);
            SetStatus(WebText.T("MFA reset for {0}. They must enroll again after signing in.", target.Username));
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostToggleDisabled(string userId, bool disabled)
    {
        var target = _auth.GetUserById(userId);
        if (target is null)
        {
            SetError(WebText.T("The user no longer exists."));
            return RedirectToPage();
        }

        try
        {
            _auth.SetDisabled(CurrentUser, target, disabled, ClientIp);
            SetStatus(WebText.T(disabled ? "{0} disabled." : "{0} enabled.", target.Username));
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostToggleAdmin(string userId, bool isAdmin)
    {
        var target = _auth.GetUserById(userId);
        if (target is null)
        {
            SetError(WebText.T("The user no longer exists."));
            return RedirectToPage();
        }

        try
        {
            _auth.SetAdmin(CurrentUser, target, isAdmin, ClientIp);
            SetStatus(WebText.T(isAdmin
                ? "{0} is now an administrator."
                : "{0} is no longer an administrator.", target.Username));
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(string userId, string? confirmUsername)
    {
        var target = _auth.GetUserById(userId);
        if (target is null)
        {
            SetError(WebText.T("The user no longer exists."));
            return RedirectToPage();
        }

        if (!string.Equals(confirmUsername?.Trim(), target.Username, StringComparison.Ordinal))
        {
            SetError(WebText.T("Type the username exactly to confirm deletion."));
            return RedirectToPage();
        }

        try
        {
            _auth.DeleteUser(CurrentUser, target, ClientIp);
        }
        catch (AuthException exception)
        {
            SetError(exception.Message);
            return RedirectToPage();
        }

        try
        {
            _data.DeleteUserData(target.Id);
            SetStatus(WebText.T("{0} and their private database were deleted.", target.Username));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDataCleanupFailure(_logger, target.Username, exception);
            SetError(WebText.T("{0} was deleted, but their data directory could not be removed.", target.Username));
        }

        return RedirectToPage();
    }

    private void Load() => Users = _auth.ListUsers();

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "The account {Username} was deleted but their data directory could not be removed.")]
    private static partial void LogDataCleanupFailure(ILogger logger, string username, Exception exception);
}
