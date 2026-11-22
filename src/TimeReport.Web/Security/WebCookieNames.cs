using TimeReport.Web.Configuration;

namespace TimeReport.Web.Security;

public static class WebCookieNames
{
    public static string Auth(WebOptions options) =>
        options.CookieSecure ? "__Host-timereport.auth" : "timereport.auth";

    public static string Mfa(WebOptions options) =>
        options.CookieSecure ? "__Host-timereport.mfa" : "timereport.mfa";

    public static string Antiforgery(WebOptions options) =>
        options.CookieSecure ? "__Host-timereport.csrf" : "timereport.csrf";

    public static string Theme() => "timereport.theme";
}
