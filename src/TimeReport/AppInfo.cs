using System.Reflection;

namespace TimeReport;

public static class AppInfo
{
    public const string Name = "TimeReport";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version is { } version
            ? version.ToString(3)
            : "0.0.0";

    public static string BuildNumber =>
        Metadata("BuildNumber") ?? "local";

    public static string BuildCommit =>
        Metadata("BuildCommit") ?? string.Empty;

    private static string? Metadata(string key) =>
        typeof(AppInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;
}
