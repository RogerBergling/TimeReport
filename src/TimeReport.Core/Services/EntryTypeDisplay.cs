using TimeReport.Models;

namespace TimeReport.Services;

public static class EntryTypeDisplay
{
    public static string DisplayName(string? name, EntryKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return kind.DisplayName();
        }

        return string.Equals(name, DefaultName(kind), StringComparison.OrdinalIgnoreCase)
            ? kind.DisplayName()
            : name;
    }

    public static string DefaultName(EntryKind kind) => kind switch
    {
        EntryKind.Work => "Work",
        EntryKind.Lunch => "Lunch",
        EntryKind.Away => "Away",
        EntryKind.XPause => "X-Pause",
        _ => string.Empty,
    };
}
