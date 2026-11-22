using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Web.Security;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    private readonly AuthCookieService _cookies;

    public LogoutModel(AuthCookieService cookies)
    {
        _cookies = cookies;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await _cookies.SignOutAsync(HttpContext);
        return RedirectToPage("/Login");
    }
}
