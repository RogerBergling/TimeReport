namespace TimeReport.Models;

public sealed record EntryType(
    long Id,
    string Name,
    EntryKind Category,
    bool Archived = false)
{
    public override string ToString() => Name;
}
