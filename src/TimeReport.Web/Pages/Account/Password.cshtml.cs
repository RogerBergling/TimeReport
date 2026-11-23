using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Localization;
using TimeReport.Web.Security;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Account;

[Authorize]
public sealed class PasswordModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;

    public PasswordModel(AuthService auth, AuthCookieService cookies)
    {
        _auth = auth;
        _cookies = cookies;
    }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public bool Forced => CurrentUser.MustChangePassword;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = WebText.T("The new passwords do not match.");
            CurrentPassword = null;
            NewPassword = null;
            ConfirmPassword = null;
            return Page();
        }

        try
        {
            var updated = _auth.ChangePassword(CurrentUser, CurrentPassword, NewPassword);
            await _cookies.SignInAsync(HttpContext, updated);
            SetStatus(WebText.T("Password changed."));
            return RedirectToPage(
                updated.TotpConfirmed ? "/Account/Security" : "/Account/Totp");
        }
        catch (AuthException exception)
        {
            ErrorMessage = exception.Message;
            CurrentPassword = null;
            NewPassword = null;
            ConfirmPassword = null;
            return Page();
        }
    }
}
