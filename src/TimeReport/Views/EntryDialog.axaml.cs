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
        Populate();
        UpdateTypeUi();
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

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e) => UpdateTypeUi();

    private void OnDateChanged(object? sender, TextChangedEventArgs e) => UpdateXPauseHint();

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
        if (_service is null)
        {
            return;
        }

        try
        {
            var day = TimeUtil.ParseDate(DateTextBox.Text ?? string.Empty);
            var start = TimeUtil.ParseTime(StartTextBox.Text ?? string.Empty);
            var end = TimeUtil.ParseTime(EndTextBox.Text ?? string.Empty);
            var selectedType = SelectedType
                ?? throw new ValidationException(LocalizationService.T("ValidationTypeMissing"));
            var kind = selectedType.Category;
            long? projectId = null;
            string? awayReason = null;

            if (kind == EntryKind.Work)
            {
                if (ProjectComboBox.SelectedItem is not Project project)
                {
                    throw new ValidationException(
                        LocalizationService.T("EntryDialogChooseProject"));
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
                TypeId: selectedType.Id);
            var saved = _service.SaveEntry(candidate);
            Close(saved);
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotSaveEntry"),
                exception.Message,
                false);
        }
        catch (DatabaseException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotSaveEntry"),
                exception.Message,
                false);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
