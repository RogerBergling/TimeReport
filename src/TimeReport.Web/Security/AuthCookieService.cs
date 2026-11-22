using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Services;

namespace TimeReport.Web.Security;

public sealed class AuthCookieService
{
    private readonly WebOptions _options;

    public AuthCookieService(WebOptions options)
    {
        _options = options;
    }

    public async Task SignInAsync(HttpContext context, AuthUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var idleExpiry = now.AddMinutes(_options.SessionMinutes);
        var absoluteExpiry = now.AddHours(_options.SessionAbsoluteHours);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName),
            new("is_admin", user.IsAdmin ? "true" : "false"),
            new("language", user.Language),
            new("security_stamp", user.SecurityStamp),
            new("totp_confirmed", user.TotpConfirmed ? "true" : "false"),
            new("auth_time", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = idleExpiry < absoluteExpiry ? idleExpiry : absoluteExpiry,
            });
        context.SetAuthUser(user);
    }

    public async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        DeleteMfaCookie(context);
    }

    public string? GetMfaToken(HttpContext context) =>
        context.Request.Cookies[WebCookieNames.Mfa(_options)];

    public void SetMfaCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append(
            WebCookieNames.Mfa(_options),
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = _options.CookieSecure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(_options.MfaMinutes),
                IsEssential = true,
            });
    }

    public void DeleteMfaCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(
            WebCookieNames.Mfa(_options),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = _options.CookieSecure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
    }
}
