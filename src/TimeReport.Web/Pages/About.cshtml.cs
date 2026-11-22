using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class AboutModel : PageModel
{
    public void OnGet()
    {
    }
}
