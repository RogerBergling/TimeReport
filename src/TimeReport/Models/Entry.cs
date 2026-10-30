namespace TimeReport.Models;

public sealed record Entry(
    long? Id,
    DateOnly Date,
    EntryKind Kind,
    int StartMinute,
    int EndMinute,
    long? ProjectId = null,
    string? ProjectName = null,
    string? AwayReason = null,
    long? TypeId = null,
    string? TypeName = null)
{
    public int DurationMinutes => EndMinute - StartMinute;
}
