using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Services;

namespace TimeReport.Web.Security;

public sealed partial class AuthCookieEvents : CookieAuthenticationEvents
{
    private readonly AuthDatabase _database;
    private readonly WebOptions _options;
    private readonly ILogger<AuthCookieEvents> _logger;

    public AuthCookieEvents(
        AuthDatabase database,
        WebOptions options,
        ILogger<AuthCookieEvents> logger)
    {
        _database = database;
        _options = options;
        _logger = logger;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await RejectAsync(context);
            return;
        }

        var user = _database.GetUserById(userId);
        if (user is null || user.Disabled)
        {
            await RejectAsync(context);
            return;
        }

        var stamp = context.Principal?.FindFirstValue("security_stamp");
        if (string.IsNullOrEmpty(stamp) || !MasterKey.FixedTimeEquals(stamp, user.SecurityStamp))
        {
            LogStampMismatch(_logger);
            await RejectAsync(context);
            return;
        }

        var authTimeClaim = context.Principal?.FindFirstValue("auth_time");
        if (!long.TryParse(
                authTimeClaim,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var authTimeUnix) ||
            DateTimeOffset.FromUnixTimeSeconds(authTimeUnix)
                .AddHours(_options.SessionAbsoluteHours) <= DateTimeOffset.UtcNow)
        {
            LogSessionExpired(_logger);
            await RejectAsync(context);
            return;
        }

        context.HttpContext.SetAuthUser(user);
        await base.ValidatePrincipal(context);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "A session was rejected because its security stamp changed.")]
    private static partial void LogStampMismatch(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "A session was rejected because its absolute lifetime expired.")]
    private static partial void LogSessionExpired(ILogger logger);
}
