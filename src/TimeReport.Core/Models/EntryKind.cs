using TimeReport.Services;

namespace TimeReport.Models;

public enum EntryKind
{
    Work,
    Lunch,
    Away,
    XPause,
}

public static class EntryKindExtensions
{
    public static string ToDbString(this EntryKind kind) => kind switch
    {
        EntryKind.Work => "work",
        EntryKind.Lunch => "lunch",
        EntryKind.Away => "away",
        EntryKind.XPause => "xpause",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static EntryKind FromDbString(string value) => value switch
    {
        "work" => EntryKind.Work,
        "lunch" => EntryKind.Lunch,
        "away" => EntryKind.Away,
        "xpause" => EntryKind.XPause,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown entry kind."),
    };

    public static string DisplayName(this EntryKind kind) => LocalizationService.T(kind switch
    {
        EntryKind.Work => "EntryKindWork",
        EntryKind.Lunch => "EntryKindLunch",
        EntryKind.Away => "EntryKindAway",
        EntryKind.XPause => "EntryKindXPause",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    });
}
