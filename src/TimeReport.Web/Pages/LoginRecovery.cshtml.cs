using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class LoginRecoveryModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;
    private readonly WebOptions _options;

    public LoginRecoveryModel(AuthService auth, AuthCookieService cookies, WebOptions options)
    {
        _auth = auth;
        _cookies = cookies;
        _options = options;
    }

    [BindProperty]
    public string? Code { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public AuthUser? PendingUser { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!LoadPending())
        {
            return RedirectToPage("/Login");
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!LoadPending())
        {
            return RedirectToPage("/Login");
        }

        ReturnUrl = returnUrl;
        var pending = _auth.GetPendingMfa(_cookies.GetMfaToken(HttpContext));
        if (pending is null || PendingUser is null)
        {
            _cookies.DeleteMfaCookie(HttpContext);
            return RedirectToPage("/Login");
        }

        if (!_auth.VerifyRecoveryCode(PendingUser, Code, ClientIp))
        {
            var attempts = _auth.RecordMfaFailure(pending.Value.Pending);
            if (attempts >= _options.MaxMfaAttempts)
            {
                _auth.ClearPendingMfa(pending.Value.Pending);
                _cookies.DeleteMfaCookie(HttpContext);
                ErrorMessage = "Too many incorrect codes. Sign in again to start a new verification.";
                PendingUser = null;
                return Page();
            }

            ErrorMessage = "That recovery code is not valid or has already been used.";
            Code = null;
            return Page();
        }

        _auth.ClearPendingMfa(pending.Value.Pending);
        _cookies.DeleteMfaCookie(HttpContext);
        await _cookies.SignInAsync(HttpContext, PendingUser);
        _auth.Audit(PendingUser.Username, "login.completed", "recovery-code", ClientIp);
        return Redirect(SafeReturnUrl(ReturnUrl ?? returnUrl) ?? "/");
    }

    private bool LoadPending()
    {
        var token = _cookies.GetMfaToken(HttpContext);
        var pending = _auth.GetPendingMfa(token);
        if (pending is null)
        {
            _cookies.DeleteMfaCookie(HttpContext);
            return false;
        }

        PendingUser = pending.Value.User;
        return true;
    }
}
