using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.ViewModels;

namespace TimeReport.Views;

public partial class MainWindow : Window
{
    private readonly AppSession? _session;
    private TimeReportService? _service;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(AppSession session) : this()
    {
        _session = session;
        RebindService(session.Service);
    }

    private void RebindService(TimeReportService service)
    {
        _service = service;
        var viewModel = new MainWindowViewModel(service);
        viewModel.AddEntryRequested += async day => await AddEntryAsync(day);
        viewModel.EditEntryRequested += async entry => await EditEntryAsync(entry);
        viewModel.DeleteEntryRequested += async entry => await DeleteEntryAsync(entry);
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnPreviousWeekClick(object? sender, RoutedEventArgs e) => _viewModel?.ChangeWeek(-1);

    private void OnNextWeekClick(object? sender, RoutedEventArgs e) => _viewModel?.ChangeWeek(1);

    private void OnThisWeekClick(object? sender, RoutedEventArgs e) => _viewModel?.GoToday();

    private async void OnAddEntryClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await AddEntryAsync(_viewModel.DefaultEntryDate());
        }
    }

    private async Task AddEntryAsync(DateOnly day)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new EntryDialog(_service, day);
        var result = await dialog.ShowDialog<Entry?>(this);
        if (result is not null)
        {
            _viewModel?.SetWeekForDate(result.Date);
        }
    }

    private async Task EditEntryAsync(Entry entry)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new EntryDialog(_service, entry.Date, entry);
        var result = await dialog.ShowDialog<Entry?>(this);
        if (result is not null)
        {
            _viewModel?.SetWeekForDate(result.Date);
        }
    }

    private async Task DeleteEntryAsync(Entry entry)
    {
        if (_service is null || entry.Id is null)
        {
            return;
        }

        var description = entry.Kind.DisplayName().ToLowerInvariant();
        if (!await MessageDialog.ShowAsync(
                this,
                "Delete entry",
                $"Delete this {description} entry?",
                true))
        {
            return;
        }

        try
        {
            _service.DeleteEntry(entry.Id.Value);
            _viewModel?.Refresh();
        }
        catch (Exception exception) when (exception is ValidationException or DatabaseException)
        {
            await MessageDialog.ShowAsync(this, "Cannot delete entry", exception.Message, false);
        }
    }

    private async void OnProjectsClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new ProjectsDialog(_service);
        await dialog.ShowDialog(this);
        _viewModel?.Refresh();
    }

    private async void OnDatabaseLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var dialog = new DatabaseLocationDialog(
            _session.Database.Path,
            _session.DefaultDatabasePath);
        var result = await dialog.ShowDialog<DatabaseLocationResult?>(this);
        if (result is null)
        {
            return;
        }

        try
        {
            var service = _session.SwitchDatabase(result.DatabasePath, result.CopyCurrentData);
            RebindService(service);
            await MessageDialog.ShowAsync(
                this,
                "Database location changed",
                $"TimeReport is now using:\n{_session.Database.Path}",
                false);
        }
        catch (Exception exception)
        {
            await MessageDialog.ShowAsync(
                this,
                "Cannot change database location",
                exception.Message,
                false);
        }
    }

    private async void OnBackupClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Backup TimeReport database",
            SuggestedFileName = $"TimeReport-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db",
            DefaultExtension = "db",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("SQLite database") { Patterns = ["*.db"] },
            ],
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var saved = _service.Database.BackupTo(file.Path.LocalPath);
            await MessageDialog.ShowAsync(
                this,
                "Backup complete",
                $"Backup saved to:\n{saved}",
                false);
        }
        catch (DatabaseException exception)
        {
            await MessageDialog.ShowAsync(this, "Backup failed", exception.Message, false);
        }
    }

    private async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Restore TimeReport database",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SQLite database")
                {
                    Patterns = ["*.db", "*.sqlite", "*.sqlite3"],
                },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        if (!await MessageDialog.ShowAsync(
                this,
                "Restore database",
                "Restoring replaces all current data with the selected backup.\n\n" +
                "A backup of the current database will be created automatically first.\n\n" +
                "Continue?",
                true))
        {
            return;
        }

        try
        {
            var autoBackup = _service.Database.RestoreFrom(files[0].Path.LocalPath);
            _viewModel?.Refresh();
            await MessageDialog.ShowAsync(
                this,
                "Restore complete",
                $"Data restored.\n\nThe previous database was backed up to:\n{autoBackup}",
                false);
        }
        catch (DatabaseException exception)
        {
            await MessageDialog.ShowAsync(this, "Restore failed", exception.Message, false);
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => _viewModel?.Refresh();

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new AboutDialog(
            _service,
            _session?.DataDirectory ?? string.Empty,
            _session?.SettingsPath ?? string.Empty);
        await dialog.ShowDialog(this);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || MainTabs.SelectedIndex != 1)
        {
            return;
        }

        if (_viewModel.Totals.SelectedPeriod == "All time")
        {
            _viewModel.Totals.Refresh();
        }
        else
        {
            _viewModel.Totals.SetAnchor(_viewModel.CurrentWeekStart);
        }
    }
}
