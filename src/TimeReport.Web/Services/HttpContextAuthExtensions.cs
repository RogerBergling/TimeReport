using TimeReport.Web.Auth;

namespace TimeReport.Web.Services;

public static class HttpContextAuthExtensions
{
    private const string ItemKey = "TimeReport.AuthUser";

    public static void SetAuthUser(this HttpContext context, AuthUser user) =>
        context.Items[ItemKey] = user;

    public static AuthUser? GetAuthUser(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as AuthUser : null;
}
