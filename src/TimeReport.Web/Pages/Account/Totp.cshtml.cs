using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Localization;
using TimeReport.Web.Security;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Account;

[Authorize]
public sealed class TotpModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;
    private readonly TotpService _totp;
    private readonly WebOptions _options;

    public TotpModel(
        AuthService auth,
        AuthCookieService cookies,
        TotpService totp,
        WebOptions options)
    {
        _auth = auth;
        _cookies = cookies;
        _totp = totp;
        _options = options;
    }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? Code { get; set; }

    public bool AlreadyConfirmed { get; private set; }

    public string? QrDataUrl { get; private set; }

    public string? SecretDisplay { get; private set; }

    public bool Forced { get; private set; }

    public IActionResult OnGet(bool forced = false)
    {
        Forced = forced;
        if (CurrentUser.TotpConfirmed)
        {
            AlreadyConfirmed = true;
            return Page();
        }

        PrepareEnrollment();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Forced = true;
        if (CurrentUser.TotpConfirmed)
        {
            return RedirectToPage("/Account/Security");
        }

        if (!_auth.VerifyCurrentPassword(CurrentUser, CurrentPassword, "totp-enroll", ClientIp))
        {
            ErrorMessage = WebText.T("The current password is not correct.");
            CurrentPassword = null;
            Code = null;
            PrepareEnrollment();
            return Page();
        }

        try
        {
            var updated = _auth.ConfirmTotp(CurrentUser, Code);
            await _cookies.SignInAsync(HttpContext, updated);
            SetStatus(WebText.T("Two-factor authentication is enabled. Save your recovery codes next."));
            return RedirectToPage("/Account/RecoveryCodes", new { required = 1 });
        }
        catch (AuthException exception)
        {
            ErrorMessage = exception.Message;
            CurrentPassword = null;
            Code = null;
            PrepareEnrollment();
            return Page();
        }
        catch (AuthDatabaseException exception)
        {
            ErrorMessage = exception.Message;
            CurrentPassword = null;
            Code = null;
            PrepareEnrollment();
            return Page();
        }
    }

    private void PrepareEnrollment()
    {
        var secret = _auth.BeginTotpEnrollment(CurrentUser);
        var uri = _totp.BuildUri(_options.Issuer, CurrentUser.Username, secret);
        QrDataUrl = _totp.QrCodeDataUrl(uri);
        SecretDisplay = TotpService.FormatSecretForDisplay(secret);
    }
}
