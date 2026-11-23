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
        // Authenticated users keep their saved language. Anonymous pages use
        // the language switch cookie or the browser's Accept-Language header.
        var language = context.GetAuthUser()?.Language;
        if (string.IsNullOrWhiteSpace(language))
        {
            var cookie = context.Request.Cookies["timereport.lang"];
            if (cookie is "en" or "sv")
            {
                language = cookie;
            }
            else if (context.Request.Headers.AcceptLanguage
                         .ToString()
                         .StartsWith("sv", StringComparison.OrdinalIgnoreCase))
            {
                language = "sv";
            }
        }

        using var scope = LocalizationService.UseLanguage(language ?? "en");
        await _next(context);
    }
}
