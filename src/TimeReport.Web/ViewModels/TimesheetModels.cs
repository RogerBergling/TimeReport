using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Web.ViewModels;

public sealed class EntryRowView
{
    public EntryRowView(Entry entry)
    {
        Entry = entry;
    }

    public Entry Entry { get; }

    public long Id => Entry.Id ?? 0;

    public string TypeText => EntryTypeDisplay.DisplayName(Entry.TypeName, Entry.Kind);

    public string TypeClass => Entry.Kind.ToString().ToLowerInvariant();

    public string DetailText => Entry.Kind switch
    {
        EntryKind.Work => Entry.ProjectName ?? "—",
        EntryKind.Away => Entry.AwayReason ?? Entry.TypeName ?? "—",
        _ => "—",
    };

    public string StartText => TimeUtil.FormatTime(Entry.StartMinute);

    public string EndText => TimeUtil.FormatTime(Entry.EndMinute);

    public string DurationText => TimeUtil.FormatDuration(Entry.DurationMinutes);
}

public sealed class TimesheetDayView
{
    public TimesheetDayView(DateOnly date, bool isToday, IReadOnlyList<EntryRowView> entries, Totals totals)
    {
        Date = date;
        IsToday = isToday;
        Entries = entries;
        Totals = totals;
    }

    public DateOnly Date { get; }

    public bool IsToday { get; }

    public IReadOnlyList<EntryRowView> Entries { get; }

    public Totals Totals { get; }

    public string Title => TimeUtil.FormatDateLong(Date);

    public string ShortTitle => TimeUtil.FormatDayShort(Date);

    public string WorkedText => TimeUtil.FormatDuration(Totals.WorkedMinutes);

    public string LunchText => TimeUtil.FormatDuration(Totals.LunchMinutes);

    public string AwayText => TimeUtil.FormatDuration(Totals.AwayMinutes);

    public string XPauseText => TimeUtil.FormatDuration(Totals.XPauseMinutes);

    public string OvertimeText => TimeUtil.FormatDuration(Totals.OvertimeMinutes);
}

public sealed class TimesheetWeekView
{
    public TimesheetWeekView(
        DateOnly start,
        DateOnly end,
        string label,
        IReadOnlyList<TimesheetDayView> days,
        Totals totals)
    {
        Start = start;
        End = end;
        Label = label;
        Days = days;
        Totals = totals;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public string Label { get; }

    public IReadOnlyList<TimesheetDayView> Days { get; }

    public Totals Totals { get; }

    public string WorkedText => TimeUtil.FormatDuration(Totals.WorkedMinutes);

    public string LunchText => TimeUtil.FormatDuration(Totals.LunchMinutes);

    public string AwayText => TimeUtil.FormatDuration(Totals.AwayMinutes);

    public string XPauseText => $"{TimeUtil.FormatDuration(Totals.XPauseMinutes)} / 1:00";

    public string OvertimeText => TimeUtil.FormatDuration(Totals.OvertimeMinutes);

    public string DaysWorkedText => Totals.DaysWorked.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class TotalsRowView
{
    public TotalsRowView(string period, Totals totals)
    {
        Period = period;
        Totals = totals;
    }

    public string Period { get; }

    public Totals Totals { get; }

    public string WorkedText => TimeUtil.FormatDuration(Totals.WorkedMinutes);

    public string LunchText => TimeUtil.FormatDuration(Totals.LunchMinutes);

    public string AwayText => TimeUtil.FormatDuration(Totals.AwayMinutes);

    public string XPauseText => TimeUtil.FormatDuration(Totals.XPauseMinutes);

    public string OvertimeText => TimeUtil.FormatDuration(Totals.OvertimeMinutes);

    public string DaysWorkedText => Totals.DaysWorked.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string DaysAwayText => Totals.DaysAway.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ProjectRowView(Project Project, int EntryCount);

public sealed record EntryTypeRowView(EntryType Type, int EntryCount)
{
    public string CategoryText => Type.Category.DisplayName();

    public string StatusText => LocalizationService.T(
        Type.Archived ? "ProjectStatusArchived" : "ProjectStatusActive");
}
