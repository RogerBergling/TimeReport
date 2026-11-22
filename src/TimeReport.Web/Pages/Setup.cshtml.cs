using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class SetupModel : AppPageModel
{
    private readonly AuthService _auth;
    private readonly AuthCookieService _cookies;
    private readonly SetupTokenService _setupToken;
    private readonly LoginThrottle _throttle;
    private readonly WebOptions _options;

    public SetupModel(
        AuthService auth,
        AuthCookieService cookies,
        SetupTokenService setupToken,
        LoginThrottle throttle,
        WebOptions options)
    {
        _auth = auth;
        _cookies = cookies;
        _setupToken = setupToken;
        _throttle = throttle;
        _options = options;
    }

    [BindProperty]
    public string? SetupToken { get; set; }

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    public IActionResult OnGet()
    {
        if (!_setupToken.IsSetupRequired())
        {
            return RedirectToPage("/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_setupToken.IsSetupRequired())
        {
            return RedirectToPage("/Login");
        }

        var window = TimeSpan.FromMinutes(_options.LoginWindowMinutes);
        var throttleKey = $"setup:{ClientIp}";
        if (_throttle.IsBlocked(throttleKey, _options.MaxSetupAttempts, window))
        {
            ErrorMessage = "Too many setup attempts. Restart the service to generate a new setup token.";
            Password = null;
            ConfirmPassword = null;
            return Page();
        }

        if (!_setupToken.Validate(SetupToken))
        {
            _throttle.RecordFailure(throttleKey, window);
            ErrorMessage = "The setup token is not valid.";
            Password = null;
            ConfirmPassword = null;
            SetupToken = null;
            return Page();
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "The passwords do not match.";
            Password = null;
            ConfirmPassword = null;
            return Page();
        }

        try
        {
            var user = _auth.CreateFirstAdmin(Username, DisplayName, Password, ClientIp);
            _setupToken.Invalidate();
            await _cookies.SignInAsync(HttpContext, user);
            return RedirectToPage("/Account/Totp", new { forced = 1 });
        }
        catch (AuthException exception)
        {
            ErrorMessage = exception.Message;
            Password = null;
            ConfirmPassword = null;
            SetupToken = null;
            return Page();
        }
        catch (AuthDatabaseException exception)
        {
            ErrorMessage = exception.Message;
            Password = null;
            ConfirmPassword = null;
            SetupToken = null;
            return Page();
        }
    }
}
