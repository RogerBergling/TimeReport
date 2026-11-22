using TimeReport.Services;

namespace TimeReport.Models;

public sealed record EntryType(
    long Id,
    string Name,
    EntryKind Category,
    bool Archived = false)
{
    public string DisplayName => EntryTypeDisplay.DisplayName(Name, Category);

    public override string ToString() => DisplayName;
}
