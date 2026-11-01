using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly TimeReportService _service;
    private DateOnly _currentWeekStart = TimeUtil.WeekStart(TimeZoneService.Today(TimeZoneInfo.Local.Id));

    public MainWindowViewModel(TimeReportService service)
    {
        _service = service;
        _currentWeekStart = TimeUtil.WeekStart(service.Today);
        Totals = new TotalsViewModel(service);
        Refresh();
    }

    public ObservableCollection<DayCardViewModel> DayCards { get; } = [];

    public TotalsViewModel Totals { get; }

    public string DatabasePath => _service.Database.Path;

    public string WeekLabel =>
        $"{LocalizationService.T("LabelWeekPrefix")} {TimeUtil.WeekNumber(_currentWeekStart)} · " +
        TimeUtil.FormatWeekRange(_currentWeekStart, _currentWeekStart.AddDays(6));

    public string WeekWorkedText { get; private set; } = "0:00";

    public string WeekLunchText { get; private set; } = "0:00";

    public string WeekAwayText { get; private set; } = "0:00";

    public string WeekXPauseText { get; private set; } = "0:00 / 1:00";

    public string WeekOvertimeText { get; private set; } = "0:00";

    public string WeekDaysWorkedText { get; private set; } = "0";

    public DateOnly CurrentWeekStart => _currentWeekStart;

    public event Action<DateOnly>? AddEntryRequested;

    public event Action<Entry>? EditEntryRequested;

    public event Action<Entry>? DeleteEntryRequested;

    public void Refresh()
    {
        var lastDay = _currentWeekStart.AddDays(6);
        var entries = _service.ListEntries(_currentWeekStart, lastDay);
        var byDate = entries
            .GroupBy(entry => entry.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var totals = _service.RangeTotals(_currentWeekStart, lastDay);

        WeekWorkedText = TimeUtil.FormatDuration(totals.WorkedMinutes);
        WeekLunchText = TimeUtil.FormatDuration(totals.LunchMinutes);
        WeekAwayText = TimeUtil.FormatDuration(totals.AwayMinutes);
        WeekXPauseText = $"{TimeUtil.FormatDuration(totals.XPauseMinutes)} / 1:00";
        WeekOvertimeText = TimeUtil.FormatDuration(totals.OvertimeMinutes);
        WeekDaysWorkedText = totals.DaysWorked.ToString();

        DayCards.Clear();
        var today = _service.Today;
        for (var offset = 0; offset < 7; offset++)
        {
            var day = _currentWeekStart.AddDays(offset);
            var dayEntries = byDate.TryGetValue(day, out var list) ? list : [];
            var dayTotals = _service.DailyTotals(day, dayEntries);
            DayCards.Add(new DayCardViewModel(
                day,
                dayEntries,
                dayTotals,
                today,
                onAdd: _ => AddEntryRequested?.Invoke(day),
                onEdit: entry => EditEntryRequested?.Invoke(entry),
                onDelete: entry => DeleteEntryRequested?.Invoke(entry)));
        }

        OnPropertyChanged(nameof(WeekLabel));
        OnPropertyChanged(nameof(WeekWorkedText));
        OnPropertyChanged(nameof(WeekLunchText));
        OnPropertyChanged(nameof(WeekAwayText));
        OnPropertyChanged(nameof(WeekXPauseText));
        OnPropertyChanged(nameof(WeekOvertimeText));
        OnPropertyChanged(nameof(WeekDaysWorkedText));
        OnPropertyChanged(nameof(CurrentWeekStart));

        Totals.Refresh();
    }

    public void ChangeWeek(int weeks)
    {
        _currentWeekStart = _currentWeekStart.AddDays(7 * weeks);
        Refresh();
    }

    public void GoToday()
    {
        _currentWeekStart = TimeUtil.WeekStart(_service.Today);
        Refresh();
    }

    public void SetWeekForDate(DateOnly day)
    {
        _currentWeekStart = TimeUtil.WeekStart(day);
        Refresh();
    }

    public DateOnly DefaultEntryDate()
    {
        var today = _service.Today;
        return _currentWeekStart <= today && today <= _currentWeekStart.AddDays(6)
            ? today
            : _currentWeekStart;
    }
}

public sealed class DayCardViewModel : ViewModelBase
{
    public DayCardViewModel(
        DateOnly day,
        IReadOnlyList<Entry> entries,
        Totals totals,
        DateOnly today,
        Action<DateOnly> onAdd,
        Action<Entry> onEdit,
        Action<Entry> onDelete)
    {
        Day = day;
        Title = TimeUtil.FormatDateLong(day);
        if (day == today)
        {
            Title += $" · {LocalizationService.T("LabelToday")}";
        }

        WorkedText = $"{LocalizationService.T("LabelWorked")} {TimeUtil.FormatDuration(totals.WorkedMinutes)}";
        LunchText = $"{LocalizationService.T("LabelLunch")} {TimeUtil.FormatDuration(totals.LunchMinutes)}";
        AwayText = $"{LocalizationService.T("LabelAway")} {TimeUtil.FormatDuration(totals.AwayMinutes)}";
        XPauseText = $"{LocalizationService.T("LabelXPause")} {TimeUtil.FormatDuration(totals.XPauseMinutes)}";
        OvertimeText = $"{LocalizationService.T("LabelOvertime")} {TimeUtil.FormatDuration(totals.OvertimeMinutes)}";
        Entries = new ObservableCollection<EntryRowViewModel>(
            entries.Select(entry => new EntryRowViewModel(entry, onEdit, onDelete)));
        IsEmpty = Entries.Count == 0;
        AddCommand = new RelayCommand(() => onAdd(day));
    }

    public DateOnly Day { get; }

    public string Title { get; }

    public string WorkedText { get; }

    public string LunchText { get; }

    public string AwayText { get; }

    public string XPauseText { get; }

    public string OvertimeText { get; }

    public ObservableCollection<EntryRowViewModel> Entries { get; }

    public bool IsEmpty { get; }

    public IRelayCommand AddCommand { get; }
}

public sealed class EntryRowViewModel
{
    public EntryRowViewModel(Entry entry, Action<Entry> onEdit, Action<Entry> onDelete)
    {
        TypeText = EntryTypeDisplay.DisplayName(entry.TypeName, entry.Kind);
        DetailText = entry.Kind switch
        {
            EntryKind.Work => entry.ProjectName ?? "—",
            EntryKind.Away => entry.AwayReason ?? entry.TypeName ?? "—",
            _ => "—",
        };
        StartText = TimeUtil.FormatTime(entry.StartMinute);
        EndText = TimeUtil.FormatTime(entry.EndMinute);
        DurationText = TimeUtil.FormatDuration(entry.DurationMinutes);
        var context = $"{TypeText} {DetailText} {StartText}–{EndText}";
        EditAutomationName = $"{LocalizationService.T("ButtonEdit")} {context}";
        DeleteAutomationName = $"{LocalizationService.T("ButtonDelete")} {context}";
        EditCommand = new RelayCommand(() => onEdit(entry));
        DeleteCommand = new RelayCommand(() => onDelete(entry));
    }

    public string TypeText { get; }

    public string DetailText { get; }

    public string StartText { get; }

    public string EndText { get; }

    public string DurationText { get; }

    public string EditAutomationName { get; }

    public string DeleteAutomationName { get; }

    public IRelayCommand EditCommand { get; }

    public IRelayCommand DeleteCommand { get; }
}
