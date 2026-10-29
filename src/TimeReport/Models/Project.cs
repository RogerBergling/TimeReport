namespace TimeReport.Models;

public sealed record Project(long Id, string Name, bool Archived = false)
{
    public override string ToString() => Name;
}
