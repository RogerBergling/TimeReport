using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Web.Auth;
using TimeReport.Web.Services;

namespace TimeReport.Web.ViewModels;

public abstract class AppPageModel : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public AuthUser CurrentUser =>
        HttpContext.GetAuthUser()
        ?? throw new InvalidOperationException("There is no authenticated user for this page.");

    public string ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    protected void SetStatus(string message) => StatusMessage = message;

    protected void SetError(string message) => ErrorMessage = message;

    protected string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
