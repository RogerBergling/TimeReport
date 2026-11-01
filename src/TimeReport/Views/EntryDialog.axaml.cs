using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class EntryDialog : Window
{
    private readonly TimeReportService? _service;
    private readonly Entry? _entry;
    private List<Project> _projects = [];
    private List<EntryType> _entryTypes = [];
    private bool _initializing = true;
    private string _lastDefaultStart = "08:00";
    private string _lastDefaultEnd = "16:00";

    public EntryDialog()
    {
        InitializeComponent();
    }

    public EntryDialog(TimeReportService service, DateOnly defaultDate, Entry? entry = null) : this()
    {
        _service = service;
        _entry = entry;
        Title = LocalizationService.T(entry is null ? "EntryDialogAddTitle" : "EntryDialogEditTitle");
        DateTextBox.Text = defaultDate.ToString("yyyy-MM-dd");
        StartTextBox.Text = "08:00";
        EndTextBox.Text = "16:00";
        LoadEntryTypes();
        LoadProjects();
        LoadTimeZones();
        Populate();
        _initializing = false;
        UpdateTypeUi();
        Opened += (_, _) =>
        {
            DateTextBox.Focus();
            DateTextBox.SelectAll();
        };
    }

    private EntryType? SelectedType => TypeComboBox.SelectedItem as EntryType;

    private EntryKind SelectedKind => SelectedType?.Category ?? EntryKind.Work;

    private void LoadEntryTypes()
    {
        if (_service is null)
        {
            return;
        }

        var selectedId = SelectedType?.Id ?? _entry?.TypeId;
        _entryTypes = _service.ListEntryTypes(includeArchived: _entry is not null).ToList();
        TypeComboBox.ItemsSource = _entryTypes;
        TypeComboBox.SelectedItem = selectedId is long id
            ? _entryTypes.FirstOrDefault(type => type.Id == id)
            : _entryTypes.FirstOrDefault(type => type.Category == EntryKind.Work)
              ?? _entryTypes.FirstOrDefault();
    }

    private static TimeZoneOption? FindTimeZone(
        IReadOnlyList<TimeZoneOption> options,
        string? id)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Id, id, StringComparison.Ordinal))
            {
                return option;
            }
        }
        return null;
    }

    private void LoadProjects()
    {
        if (_service is null)
        {
            return;
        }

        _projects = _service.ListProjects(includeArchived: _entry is not null).ToList();
        ProjectComboBox.ItemsSource = _projects;
        if (_entry?.ProjectId is long projectId)
        {
            ProjectComboBox.SelectedItem = _projects.FirstOrDefault(project => project.Id == projectId);
        }
        else if (_projects.Count > 0)
        {
            ProjectComboBox.SelectedItem = _projects[0];
        }
    }

    private void LoadTimeZones()
    {
        var options = TimeZoneService.GetOptions();
        TimeZoneComboBox.ItemsSource = options;
        var selectedId = _entry?.TimeZoneId ?? _service?.TimeZoneId;
        TimeZoneComboBox.SelectedItem = FindTimeZone(options, selectedId)
            ?? (options.Count > 0 ? options[0] : null);
    }

    private void Populate()
    {
        if (_entry is null)
        {
            return;
        }

        DateTextBox.Text = _entry.Date.ToString("yyyy-MM-dd");
        StartTextBox.Text = TimeUtil.FormatTime(_entry.StartMinute);
        EndTextBox.Text = TimeUtil.FormatTime(_entry.EndMinute);
        TypeComboBox.SelectedItem = _entry.TypeId is long typeId
            ? _entryTypes.FirstOrDefault(type => type.Id == typeId)
            : _entryTypes.FirstOrDefault(type => type.Category == _entry.Kind);
        if (_entry.AwayReason is not null)
        {
            AwayReasonTextBox.Text = _entry.AwayReason;
        }
    }

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (!_initializing)
        {
            ApplyTypeDefaults();
        }
        UpdateTypeUi();
    }

    private void ApplyTypeDefaults()
    {
        var (start, end) = SelectedKind switch
        {
            EntryKind.Lunch => ("12:00", "12:30"),
            EntryKind.XPause => ("10:00", "10:15"),
            _ => ("08:00", "16:00"),
        };

        var startText = StartTextBox.Text ?? string.Empty;
        var endText = EndTextBox.Text ?? string.Empty;
        if ((string.Equals(startText, _lastDefaultStart, StringComparison.Ordinal) &&
             string.Equals(endText, _lastDefaultEnd, StringComparison.Ordinal)) ||
            string.IsNullOrWhiteSpace(startText) ||
            string.IsNullOrWhiteSpace(endText))
        {
            StartTextBox.Text = start;
            EndTextBox.Text = end;
        }

        _lastDefaultStart = start;
        _lastDefaultEnd = end;
    }

    private void OnDateChanged(object? sender, TextChangedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        UpdateXPauseHint();
    }

    private void UpdateTypeUi()
    {
        if (InfoText is null)
        {
            return;
        }

        var kind = SelectedKind;
        ProjectRow.IsVisible = kind == EntryKind.Work;
        AwayRow.IsVisible = kind == EntryKind.Away;
        InfoText.Text = kind switch
        {
            EntryKind.Work => LocalizationService.T("EntryDialogInfoWork"),
            EntryKind.Lunch => LocalizationService.T("EntryDialogInfoLunch"),
            EntryKind.Away => LocalizationService.T("EntryDialogInfoAway"),
            EntryKind.XPause => LocalizationService.T("EntryDialogInfoXPause"),
            _ => string.Empty,
        };
        UpdateXPauseHint();
    }

    private void UpdateXPauseHint()
    {
        if (XPauseHintText is null || _service is null)
        {
            return;
        }

        if (SelectedKind != EntryKind.XPause)
        {
            XPauseHintText.Text = string.Empty;
            return;
        }

        DateOnly day;
        try
        {
            day = TimeUtil.ParseDate(DateTextBox.Text ?? string.Empty);
        }
        catch (ValidationException)
        {
            XPauseHintText.Text = LocalizationService.T("EntryDialogInvalidDate");
            return;
        }

        var remaining = _service.XPauseRemaining(day, _entry?.Id);
        var used = TimeUtil.XPauseWeeklyLimit - remaining;
        var weekdayNote = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? LocalizationService.T("EntryDialogXPauseWeekdayNote")
            : string.Empty;
        XPauseHintText.Text = LocalizationService.T(
            "EntryDialogXPauseHint",
            TimeUtil.FormatDuration(used),
            TimeUtil.FormatDuration(remaining),
            weekdayNote);
    }

    private async void OnManageTypesClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new EntryTypesDialog(_service);
        await dialog.ShowDialog(this);
        LoadEntryTypes();
        UpdateTypeUi();
    }

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(
            this,
            LocalizationService.T("NewProjectTitle"),
            LocalizationService.T("ProjectNamePrompt"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var project = _service.CreateProject(name);
            LoadProjects();
            ProjectComboBox.SelectedItem = _projects.FirstOrDefault(item => item.Id == project.Id);
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotCreateProject"),
                exception.Message,
                false);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || !TryReadInput(out var day, out var start, out var end, out var selectedType))
        {
            return;
        }

        try
        {
            var kind = selectedType.Category;
            long? projectId = null;
            string? awayReason = null;

            if (kind == EntryKind.Work)
            {
                if (ProjectComboBox.SelectedItem is not Project project)
                {
                    SetValidation(
                        LocalizationService.T("EntryDialogChooseProject"),
                        ProjectComboBox);
                    return;
                }
                projectId = project.Id;
            }
            else if (kind == EntryKind.Away)
            {
                awayReason = AwayReasonTextBox.Text;
            }

            var candidate = new Entry(
                _entry?.Id,
                day,
                kind,
                start,
                end,
                projectId,
                AwayReason: awayReason,
                TypeId: selectedType.Id,
                TimeZoneId: (TimeZoneComboBox.SelectedItem as TimeZoneOption)?.Id
                            ?? _service.TimeZoneId);
            var saved = _service.SaveEntry(candidate);
            Close(saved);
        }
        catch (ValidationException exception)
        {
            SetValidation(exception.Message);
        }
        catch (DatabaseException exception)
        {
            AppLog.Error("Could not save entry.", exception);
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotSaveEntry"),
                exception.Message,
                false);
        }
    }

    private bool TryReadInput(
        out DateOnly day,
        out int start,
        out int end,
        out EntryType selectedType)
    {
        day = default;
        start = 0;
        end = 0;
        selectedType = null!;
        ValidationText.Text = string.Empty;

        try
        {
            day = TimeUtil.ParseDate(DateTextBox.Text ?? string.Empty);
        }
        catch (ValidationException exception)
        {
            SetValidation(exception.Message, DateTextBox);
            return false;
        }

        try
        {
            start = TimeUtil.ParseTime(StartTextBox.Text ?? string.Empty);
        }
        catch (ValidationException exception)
        {
            SetValidation(exception.Message, StartTextBox);
            return false;
        }

        try
        {
            end = TimeUtil.ParseTime(EndTextBox.Text ?? string.Empty);
        }
        catch (ValidationException exception)
        {
            SetValidation(exception.Message, EndTextBox);
            return false;
        }

        if (end <= start)
        {
            SetValidation(LocalizationService.T("ValidationEndAfterStart"), EndTextBox);
            return false;
        }

        if (SelectedType is not EntryType type)
        {
            SetValidation(LocalizationService.T("ValidationTypeMissing"), TypeComboBox);
            return false;
        }
        selectedType = type;
        return true;
    }

    private void SetValidation(string message, Control? focus = null)
    {
        ValidationText.Text = message;
        focus?.Focus();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
