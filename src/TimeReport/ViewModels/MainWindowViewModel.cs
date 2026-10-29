using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly TimeReportService _service;
    private DateOnly _currentWeekStart = TimeUtil.WeekStart(DateOnly.FromDateTime(DateTime.Today));

    public MainWindowViewModel(TimeReportService service)
    {
        _service = service;
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
        WeekDaysWorkedText = totals.DaysWorked.ToString();

        DayCards.Clear();
        for (var offset = 0; offset < 7; offset++)
        {
            var day = _currentWeekStart.AddDays(offset);
            var dayEntries = byDate.TryGetValue(day, out var list) ? list : [];
            var dayTotals = _service.DailyTotals(day, dayEntries);
            DayCards.Add(new DayCardViewModel(
                day,
                dayEntries,
                dayTotals,
                onAdd: _ => AddEntryRequested?.Invoke(day),
                onEdit: entry => EditEntryRequested?.Invoke(entry),
                onDelete: entry => DeleteEntryRequested?.Invoke(entry)));
        }

        OnPropertyChanged(nameof(WeekLabel));
        OnPropertyChanged(nameof(WeekWorkedText));
        OnPropertyChanged(nameof(WeekLunchText));
        OnPropertyChanged(nameof(WeekAwayText));
        OnPropertyChanged(nameof(WeekXPauseText));
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
        _currentWeekStart = TimeUtil.WeekStart(DateOnly.FromDateTime(DateTime.Today));
        Refresh();
    }

    public void SetWeekForDate(DateOnly day)
    {
        _currentWeekStart = TimeUtil.WeekStart(day);
        Refresh();
    }

    public DateOnly DefaultEntryDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
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
        Action<DateOnly> onAdd,
        Action<Entry> onEdit,
        Action<Entry> onDelete)
    {
        Day = day;
        Title = TimeUtil.FormatDateLong(day);
        if (day == DateOnly.FromDateTime(DateTime.Today))
        {
            Title += $" · {LocalizationService.T("LabelToday")}";
        }

        WorkedText = $"{LocalizationService.T("LabelWorked")} {TimeUtil.FormatDuration(totals.WorkedMinutes)}";
        LunchText = $"{LocalizationService.T("LabelLunch")} {TimeUtil.FormatDuration(totals.LunchMinutes)}";
        AwayText = $"{LocalizationService.T("LabelAway")} {TimeUtil.FormatDuration(totals.AwayMinutes)}";
        XPauseText = $"{LocalizationService.T("LabelXPause")} {TimeUtil.FormatDuration(totals.XPauseMinutes)}";
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

    public ObservableCollection<EntryRowViewModel> Entries { get; }

    public bool IsEmpty { get; }

    public IRelayCommand AddCommand { get; }
}

public sealed class EntryRowViewModel
{
    public EntryRowViewModel(Entry entry, Action<Entry> onEdit, Action<Entry> onDelete)
    {
        TypeText = entry.Kind.DisplayName();
        DetailText = entry.ProjectName ?? entry.AwayReason ?? "—";
        StartText = TimeUtil.FormatTime(entry.StartMinute);
        EndText = TimeUtil.FormatTime(entry.EndMinute);
        DurationText = TimeUtil.FormatDuration(entry.DurationMinutes);
        EditCommand = new RelayCommand(() => onEdit(entry));
        DeleteCommand = new RelayCommand(() => onDelete(entry));
    }

    public string TypeText { get; }

    public string DetailText { get; }

    public string StartText { get; }

    public string EndText { get; }

    public string DurationText { get; }

    public IRelayCommand EditCommand { get; }

    public IRelayCommand DeleteCommand { get; }
}
