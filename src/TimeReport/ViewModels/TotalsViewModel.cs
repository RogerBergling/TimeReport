using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.ViewModels;

public sealed class TotalsViewModel : ViewModelBase
{
    private readonly TimeReportService _service;
    private DateOnly _anchor = DateOnly.FromDateTime(DateTime.Today);
    private string _periodCode = "Week";
    private bool _canNavigate = true;

    public TotalsViewModel(TimeReportService service)
    {
        _service = service;
        PreviousCommand = new RelayCommand(() => Move(-1));
        NextCommand = new RelayCommand(() => Move(1));
        CurrentCommand = new RelayCommand(Current);
        RefreshPeriodOptions();
        Refresh();
    }

    public ObservableCollection<PeriodOption> PeriodOptions { get; } = [];

    public PeriodOption? SelectedPeriodOption
    {
        get => PeriodOptions.FirstOrDefault(option => option.Code == _periodCode);
        set
        {
            if (value is null || value.Code == _periodCode)
            {
                return;
            }
            _periodCode = value.Code;
            OnPropertyChanged();
            Refresh();
        }
    }

    public string SelectedPeriod => _periodCode;

    public string PeriodLabel { get; private set; } = string.Empty;

    public string WorkedText { get; private set; } = "0:00";

    public string LunchText { get; private set; } = "0:00";

    public string AwayText { get; private set; } = "0:00";

    public string XPauseText { get; private set; } = "0:00";

    public string DaysWorkedText { get; private set; } = "0";

    public string DaysAwayText { get; private set; } = "0";

    public bool CanNavigate
    {
        get => _canNavigate;
        private set => SetProperty(ref _canNavigate, value);
    }

    public ObservableCollection<TotalsRowViewModel> Rows { get; } = [];

    public IRelayCommand PreviousCommand { get; }

    public IRelayCommand NextCommand { get; }

    public IRelayCommand CurrentCommand { get; }

    public void SetAnchor(DateOnly day)
    {
        _anchor = day;
        Refresh();
    }

    public void RefreshLocalization()
    {
        RefreshPeriodOptions();
        Refresh();
    }

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<TotalsRowViewModel>();
        var totals = new Totals();

        switch (_periodCode)
        {
            case "Month":
                {
                    CanNavigate = true;
                    var (start, end) = TimeUtil.MonthBounds(_anchor);
                    PeriodLabel = $"{TimeUtil.FormatMonthName(start)} {start.Year}";
                    foreach (var (day, dayTotals) in _service.TotalsByDay(start, end))
                    {
                        var label = TimeUtil.FormatDayShort(day);
                        if (day == today)
                        {
                            label += $" · {LocalizationService.T("LabelToday")}";
                        }
                        rows.Add(new TotalsRowViewModel(label, dayTotals));
                    }
                    totals = _service.RangeTotals(start, end);
                    break;
                }
            case "Year":
                {
                    CanNavigate = true;
                    var (start, end) = TimeUtil.YearBounds(_anchor);
                    PeriodLabel = start.Year.ToString();
                    foreach (var (monthStart, monthTotals) in _service.TotalsByMonth(start.Year))
                    {
                        rows.Add(new TotalsRowViewModel(TimeUtil.FormatMonthName(monthStart), monthTotals));
                    }
                    totals = _service.RangeTotals(start, end);
                    break;
                }
            case "All time":
                {
                    CanNavigate = false;
                    var allTime = LocalizationService.T("PeriodAllTime");
                    var bounds = _service.AllTimeBounds();
                    if (bounds is null)
                    {
                        PeriodLabel = allTime;
                        totals = new Totals();
                    }
                    else
                    {
                        var (start, end) = bounds.Value;
                        PeriodLabel = start.Year == end.Year
                            ? $"{allTime} · {start.Year}"
                            : $"{allTime} · {start.Year}–{end.Year}";
                        foreach (var (yearStart, yearTotals) in _service.TotalsByYear(start.Year, end.Year))
                        {
                            rows.Add(new TotalsRowViewModel(yearStart.Year.ToString(), yearTotals));
                        }
                        totals = _service.RangeTotals(start, end);
                    }
                    break;
                }
            default:
                {
                    CanNavigate = true;
                    var start = TimeUtil.WeekStart(_anchor);
                    var end = start.AddDays(6);
                    PeriodLabel =
                        $"{LocalizationService.T("LabelWeekPrefix")} {TimeUtil.WeekNumber(start)} · " +
                        TimeUtil.FormatWeekRange(start, end);
                    foreach (var (day, dayTotals) in _service.TotalsByDay(start, end))
                    {
                        var label = TimeUtil.FormatDayShort(day);
                        if (day == today)
                        {
                            label += $" · {LocalizationService.T("LabelToday")}";
                        }
                        rows.Add(new TotalsRowViewModel(label, dayTotals));
                    }
                    totals = _service.RangeTotals(start, end);
                    break;
                }
        }

        WorkedText = TimeUtil.FormatDuration(totals.WorkedMinutes);
        LunchText = TimeUtil.FormatDuration(totals.LunchMinutes);
        AwayText = TimeUtil.FormatDuration(totals.AwayMinutes);
        XPauseText = TimeUtil.FormatDuration(totals.XPauseMinutes);
        DaysWorkedText = totals.DaysWorked.ToString();
        DaysAwayText = totals.DaysAway.ToString();

        Rows.Clear();
        if (rows.Count == 0)
        {
            Rows.Add(new TotalsRowViewModel(LocalizationService.T("EmptyEntries"), new Totals()));
        }
        else
        {
            foreach (var row in rows)
            {
                Rows.Add(row);
            }
        }

        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(WorkedText));
        OnPropertyChanged(nameof(LunchText));
        OnPropertyChanged(nameof(AwayText));
        OnPropertyChanged(nameof(XPauseText));
        OnPropertyChanged(nameof(DaysWorkedText));
        OnPropertyChanged(nameof(DaysAwayText));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(SelectedPeriodOption));
    }

    private void RefreshPeriodOptions()
    {
        var current = _periodCode;
        PeriodOptions.Clear();
        PeriodOptions.Add(new PeriodOption("Week", LocalizationService.T("PeriodWeek")));
        PeriodOptions.Add(new PeriodOption("Month", LocalizationService.T("PeriodMonth")));
        PeriodOptions.Add(new PeriodOption("Year", LocalizationService.T("PeriodYear")));
        PeriodOptions.Add(new PeriodOption("All time", LocalizationService.T("PeriodAllTime")));
        _periodCode = current;
        OnPropertyChanged(nameof(SelectedPeriodOption));
    }

    private void Move(int direction)
    {
        if (!CanNavigate)
        {
            return;
        }

        switch (_periodCode)
        {
            case "Month":
                {
                    var moved = TimeUtil.AddMonths(_anchor, direction);
                    _anchor = new DateOnly(moved.Year, moved.Month, 1);
                    break;
                }
            case "Year":
                _anchor = new DateOnly(_anchor.Year + direction, 1, 1);
                break;
            default:
                _anchor = _anchor.AddDays(7 * direction);
                break;
        }
        Refresh();
    }

    private void Current()
    {
        _anchor = DateOnly.FromDateTime(DateTime.Today);
        Refresh();
    }
}

public sealed record PeriodOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed class TotalsRowViewModel
{
    public TotalsRowViewModel(string period, Totals totals)
    {
        Period = period;
        WorkedText = TimeUtil.FormatDuration(totals.WorkedMinutes);
        LunchText = TimeUtil.FormatDuration(totals.LunchMinutes);
        AwayText = TimeUtil.FormatDuration(totals.AwayMinutes);
        XPauseText = TimeUtil.FormatDuration(totals.XPauseMinutes);
        DaysWorkedText = totals.DaysWorked.ToString();
        DaysAwayText = totals.DaysAway.ToString();
    }

    public string Period { get; }

    public string WorkedText { get; }

    public string LunchText { get; }

    public string AwayText { get; }

    public string XPauseText { get; }

    public string DaysWorkedText { get; }

    public string DaysAwayText { get; }
}
