namespace TimeReport.Web.Services;

/// <summary>
/// Keeps authenticated accounts inside the mandatory setup flow:
/// temporary password -> TOTP enrollment -> recovery codes -> application.
/// </summary>
public sealed class TotpGateMiddleware
{
    private readonly RequestDelegate _next;

    public TotpGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.GetAuthUser();
        if (user is null)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        if (path.StartsWithSegments("/logout"))
        {
            await _next(context);
            return;
        }

        if (user.MustChangePassword)
        {
            if (path.StartsWithSegments("/account/password"))
            {
                await _next(context);
                return;
            }

            context.Response.Redirect("/account/password?forced=1");
            return;
        }

        if (!user.TotpConfirmed)
        {
            if (path.StartsWithSegments("/account/totp"))
            {
                await _next(context);
                return;
            }

            context.Response.Redirect("/account/totp?forced=1");
            return;
        }

        if (user.UnusedRecoveryCodes == 0)
        {
            if (path.StartsWithSegments("/account/recovery-codes"))
            {
                await _next(context);
                return;
            }

            context.Response.Redirect("/account/recovery-codes?required=1");
            return;
        }

        await _next(context);
    }
}
