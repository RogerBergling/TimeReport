using TimeReport.Services;

namespace TimeReport.Web.Services;

public sealed class LocalizationMiddleware
{
    private readonly RequestDelegate _next;

    public LocalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // The web UI is English-only for now. The desktop app keeps its
        // English/Swedish switch. This still scopes localization state per
        // request so adding web languages later cannot race between users.
        var language = context.GetAuthUser()?.Language ?? "en";
        using var scope = LocalizationService.UseLanguage(language);
        await _next(context);
    }
}
