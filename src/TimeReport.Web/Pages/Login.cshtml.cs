using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Security;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class LoginModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;
    private readonly SetupTokenService _setup;

    public LoginModel(AuthService auth, AuthCookieService cookies, SetupTokenService setup)
    {
        _auth = auth;
        _cookies = cookies;
        _setup = setup;
    }

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(SafeReturnUrl(returnUrl) ?? "/");
        }

        if (_setup.IsSetupRequired())
        {
            return RedirectToPage("/Setup");
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_setup.IsSetupRequired())
        {
            return RedirectToPage("/Setup");
        }

        var result = _auth.AuthenticatePassword(Username, Password, ClientIp);
        if (result.Status != PasswordLoginStatus.Success || result.User is null)
        {
            ErrorMessage = result.Status == PasswordLoginStatus.Throttled
                ? "Too many sign-in attempts. Wait a few minutes and try again."
                : "Invalid username or password.";
            Password = null;
            return Page();
        }

        if (result.User.TotpConfirmed)
        {
            var token = _auth.CreatePendingMfa(result.User);
            _cookies.SetMfaCookie(HttpContext, token);
            return RedirectToPage("/LoginMfa", new { returnUrl = SafeReturnUrl(ReturnUrl) });
        }

        await _cookies.SignInAsync(HttpContext, result.User);
        return RedirectToPage(
            result.User.MustChangePassword ? "/Account/Password" : "/Account/Totp",
            new { forced = 1 });
    }
}
