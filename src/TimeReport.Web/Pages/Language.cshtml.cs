using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Web.Configuration;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class LanguageModel : PageModel
{
    private readonly WebOptions _options;

    public LanguageModel(WebOptions options)
    {
        _options = options;
    }

    public IActionResult OnGet(string? code, string? returnUrl)
    {
        var language = string.Equals(code, "sv", StringComparison.OrdinalIgnoreCase) ? "sv" : "en";
        Response.Cookies.Append(
            "timereport.lang",
            language,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = _options.CookieSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(365),
                IsEssential = true,
            });

        var target = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/";
        return Redirect(target);
    }
}
