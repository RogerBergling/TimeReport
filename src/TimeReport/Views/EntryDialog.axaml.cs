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

    public EntryDialog()
    {
        InitializeComponent();
    }

    public EntryDialog(TimeReportService service, DateOnly defaultDate, Entry? entry = null) : this()
    {
        _service = service;
        _entry = entry;
        Title = entry is null ? "Add entry" : "Edit entry";
        TypeComboBox.ItemsSource = Enum.GetValues<EntryKind>()
            .Select(kind => new EntryKindOption(kind, kind.DisplayName()))
            .ToList();
        DateTextBox.Text = defaultDate.ToString("yyyy-MM-dd");
        StartTextBox.Text = "08:00";
        EndTextBox.Text = "16:00";
        LoadProjects();
        Populate();
        UpdateTypeUi();
    }

    private EntryKind SelectedKind =>
        (TypeComboBox.SelectedItem as EntryKindOption)?.Kind ?? EntryKind.Work;

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
            TypeComboBox.SelectedItem = ((IEnumerable<EntryKindOption>)TypeComboBox.ItemsSource!)
                .First(option => option.Kind == EntryKind.Work);
            return;
        }

        DateTextBox.Text = _entry.Date.ToString("yyyy-MM-dd");
        StartTextBox.Text = TimeUtil.FormatTime(_entry.StartMinute);
        EndTextBox.Text = TimeUtil.FormatTime(_entry.EndMinute);
        TypeComboBox.SelectedItem = ((IEnumerable<EntryKindOption>)TypeComboBox.ItemsSource!)
            .First(option => option.Kind == _entry.Kind);
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
            EntryKind.Work =>
                "Work time is counted. Lunch that overlaps work is deducted; X-Pause is tracked separately.",
            EntryKind.Lunch => "Lunch is deducted from work time when it overlaps a work entry.",
            EntryKind.Away => "Away time is tracked separately and is not counted as work.",
            EntryKind.XPause =>
                "X-Pause is a separate weekly benefit. It must be inside work hours, " +
                "in 15-minute steps, max 1:00 per week, and is not deducted from worked time.",
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
            XPauseHintText.Text = "Enter a valid date to see the weekly X-Pause balance.";
            return;
        }

        var remaining = _service.XPauseRemaining(day, _entry?.Id);
        var used = TimeUtil.XPauseWeeklyLimit - remaining;
        var weekdayNote = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? " X-Pause is only available Monday–Friday."
            : string.Empty;
        XPauseHintText.Text =
            $"X-Pause this week: {TimeUtil.FormatDuration(used)} used of 1:00, " +
            $"{TimeUtil.FormatDuration(remaining)} remaining.{weekdayNote}";
    }

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(this, "New project", "Project name:");
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
            await MessageDialog.ShowAsync(this, "Cannot create project", exception.Message, false);
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
            var kind = SelectedKind;
            long? projectId = null;
            string? awayReason = null;

            if (kind == EntryKind.Work)
            {
                if (ProjectComboBox.SelectedItem is not Project project)
                {
                    throw new ValidationException(
                        "Choose a project. Use “New…” to create one if needed.");
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
                AwayReason: awayReason);
            var saved = _service.SaveEntry(candidate);
            Close(saved);
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(this, "Cannot save entry", exception.Message, false);
        }
        catch (DatabaseException exception)
        {
            await MessageDialog.ShowAsync(this, "Cannot save entry", exception.Message, false);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}

public sealed record EntryKindOption(EntryKind Kind, string Label)
{
    public override string ToString() => Label;
}
