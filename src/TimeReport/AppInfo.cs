namespace TimeReport;

public static class AppInfo
{
    public const string Name = "TimeReport";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version is { } version
            ? version.ToString(3)
            : "0.0.0";
}
