namespace TimeReport.Services;

public sealed record TimeZoneOption(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public static class TimeZoneService
{
    public static IReadOnlyList<TimeZoneOption> GetOptions()
    {
        try
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(zone => zone.BaseUtcOffset)
                .ThenBy(zone => zone.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(zone => new TimeZoneOption(
                    zone.Id,
                    $"{zone.DisplayName} ({zone.Id})"))
                .ToList();
        }
        catch (TimeZoneNotFoundException)
        {
            return [new TimeZoneOption("UTC", "UTC (UTC)")];
        }
    }

    public static string ResolveId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local.Id;
        }

        if (TryFind(id, out var zone))
        {
            return zone.Id;
        }

        if (OperatingSystem.IsWindows() &&
            TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) &&
            TryFind(windowsId, out zone))
        {
            return zone.Id;
        }

        if (!OperatingSystem.IsWindows() &&
            TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) &&
            TryFind(ianaId, out zone))
        {
            return zone.Id;
        }

        return TimeZoneInfo.Local.Id;
    }

    public static TimeZoneInfo Resolve(string? id) =>
        TryFind(ResolveId(id), out var zone) ? zone : TimeZoneInfo.Local;

    public static DateOnly Today(string? id) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Resolve(id)).DateTime);

    public static DateTimeOffset Now(string? id) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Resolve(id));

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }
        return TryFind(id, out _) ||
               (OperatingSystem.IsWindows() && TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win) && TryFind(win, out _)) ||
               (!OperatingSystem.IsWindows() && TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) && TryFind(iana, out _));
    }

    private static bool TryFind(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }
        zone = TimeZoneInfo.Local;
        return false;
    }
}
