using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly IDropboxSyncService? _dropboxSync;
    private readonly DropboxTokenProvider? _tokenProvider;
    private readonly DropboxSyncCoordinator? _syncCoordinator;
    private TimeReportService? _service;
    private MainWindowViewModel? _viewModel;
    private bool _initializingLanguage;
    private DropboxSyncStatus _dropboxStatus = DropboxSyncStatus.NotConfigured;
    private string? _dropboxStatusDetail;
    private Entry? _lastDeletedEntry;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnMainKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => ConstrainToScreen();
    }

    private void ConstrainToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var maxWidth = screen.WorkingArea.Width / scale;
        var maxHeight = screen.WorkingArea.Height / scale;
        Width = Math.Min(Width, Math.Max(MinWidth, maxWidth));
        Height = Math.Min(Height, Math.Max(MinHeight, maxHeight));
    }

    public MainWindow(AppSession session) : this()
    {
        _session = session;
        _dropboxSync = new DropboxSyncService();
        _tokenProvider = new DropboxTokenProvider(session);
        _syncCoordinator = new DropboxSyncCoordinator(session, _dropboxSync, _tokenProvider);
        RebindService(session.Service);
        InitializeLanguageSelector();
        InitializeDropboxStatus();
        Opened += async (_, _) => await AutoSyncOnStartupAsync();
    }

    private void InitializeLanguageSelector()
    {
        _initializingLanguage = true;
        LanguageComboBox.ItemsSource = LocalizationService.Languages;
        LanguageComboBox.SelectedItem = LocalizationService.Languages
            .FirstOrDefault(language => language.Code == LocalizationService.CurrentLanguage);
        _initializingLanguage = false;
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializingLanguage ||
            LanguageComboBox.SelectedItem is not LanguageOption language ||
            language.Code == LocalizationService.CurrentLanguage)
        {
            return;
        }

        LocalizationService.SetLanguage(language.Code);
        if (_session is not null)
        {
            _session.Settings.Language = language.Code;
            _session.Settings.Save(_session.SettingsPath);
        }
        _viewModel?.Totals.RefreshLocalization();
        _viewModel?.Refresh();
        RefreshDropboxStatusText();
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
        _lastDeletedEntry = null;
        UndoDeleteButton.IsVisible = false;
    }

    private void InitializeDropboxStatus()
    {
        if (_syncCoordinator is null || !_syncCoordinator.IsConfigured)
        {
            SetDropboxStatus(DropboxSyncStatus.NotConfigured);
        }
        else if (!_syncCoordinator.AutoSyncEnabled)
        {
            SetDropboxStatus(DropboxSyncStatus.Disabled);
        }
        else
        {
            SetDropboxStatus(DropboxSyncStatus.Ready);
        }
    }

    private void SetDropboxStatus(DropboxSyncStatus status, string? detail = null)
    {
        _dropboxStatus = status;
        _dropboxStatusDetail = detail;
        RefreshDropboxStatusText();
    }

    private void RefreshDropboxStatusText()
    {
        if (DropboxStatusText is null)
        {
            return;
        }

        var key = _dropboxStatus switch
        {
            DropboxSyncStatus.NotConfigured => "DropboxStatusNotConfigured",
            DropboxSyncStatus.Disabled => "DropboxStatusDisabled",
            DropboxSyncStatus.Ready => "DropboxStatusReady",
            DropboxSyncStatus.UpToDate => "DropboxStatusUpToDate",
            DropboxSyncStatus.Uploaded or DropboxSyncStatus.Downloaded => "DropboxStatusSynced",
            DropboxSyncStatus.Conflict => "DropboxStatusConflict",
            DropboxSyncStatus.Error => "DropboxStatusError",
            _ => "DropboxStatusOff",
        };
        DropboxStatusText.Text = LocalizationService.T(key);
        ToolTip.SetTip(DropboxStatusText, _dropboxStatusDetail);
    }

    private async void OnDropboxClick(object? sender, RoutedEventArgs e)
    {
        if (_session is null || _dropboxSync is null || _tokenProvider is null)
        {
            return;
        }

        var dialog = new DropboxSyncDialog(_session, _dropboxSync, _tokenProvider);
        var saved = await dialog.ShowDialog<bool>(this);
        if (!saved)
        {
            return;
        }

        InitializeDropboxStatus();
        if (_syncCoordinator?.AutoSyncEnabled == true)
        {
            await AutoSyncOnStartupAsync();
        }
    }

    private async void OnSyncNowClick(object? sender, RoutedEventArgs e) =>
        await RunSyncNowAsync(interactive: true);

    private async Task RunSyncNowAsync(bool interactive)
    {
        if (_syncCoordinator is null)
        {
            return;
        }

        SetDropboxStatus(DropboxSyncStatus.Syncing);
        var result = await _syncCoordinator.SyncNowAsync(ResolveDropboxConflictAsync);
        if (result.Status == DropboxSyncStatus.Downloaded)
        {
            _viewModel?.Totals.RefreshLocalization();
            _viewModel?.Refresh();
        }
        SetDropboxStatus(result.Status, result.Message);
        LogSyncResult(result);

        if (interactive)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("DropboxSyncComplete"),
                result.Message,
                false);
        }
    }

    private async Task AutoSyncAfterChangeAsync()
    {
        if (_syncCoordinator is null)
        {
            return;
        }

        var result = await _syncCoordinator.AutoSyncAfterChangeAsync();
        if (result.Status == DropboxSyncStatus.Downloaded)
        {
            _viewModel?.Totals.RefreshLocalization();
            _viewModel?.Refresh();
        }
        SetDropboxStatus(result.Status, result.Message);
        LogSyncResult(result);
    }

    private async Task AutoSyncOnStartupAsync()
    {
        if (_syncCoordinator is null)
        {
            return;
        }

        var result = await _syncCoordinator.AutoSyncOnStartupAsync();
        if (result.Status == DropboxSyncStatus.Downloaded)
        {
            _viewModel?.Totals.RefreshLocalization();
            _viewModel?.Refresh();
        }
        SetDropboxStatus(result.Status, result.Message);
        LogSyncResult(result);
    }

    private static void LogSyncResult(DropboxSyncResult result)
    {
        if (result.Status == DropboxSyncStatus.Error)
        {
            AppLog.Error($"Dropbox sync failed: {result.Message}");
        }
    }

    private async Task<DropboxConflictChoice> ResolveDropboxConflictAsync()
    {
        if (await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("DropboxConflictTitle"),
                LocalizationService.T("DropboxConflictUpload"),
                true))
        {
            return DropboxConflictChoice.UseLocal;
        }

        if (await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("DropboxConflictTitle"),
                LocalizationService.T("DropboxConflictDownload"),
                true))
        {
            return DropboxConflictChoice.UseRemote;
        }

        return DropboxConflictChoice.Cancel;
    }

    private void OnMainKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.Left or Key.Right)
        {
            _viewModel?.ChangeWeek(e.Key == Key.Left ? -1 : 1);
            e.Handled = true;
        }
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
            await AutoSyncAfterChangeAsync();
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
            await AutoSyncAfterChangeAsync();
        }
    }

    private async Task DeleteEntryAsync(Entry entry)
    {
        if (_service is null || entry.Id is null)
        {
            return;
        }

        var description = EntryTypeDisplay.DisplayName(entry.TypeName, entry.Kind).ToLowerInvariant();
        if (!await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageDeleteEntryTitle"),
                LocalizationService.T("MessageDeleteEntry", description),
                true))
        {
            return;
        }

        try
        {
            _service.DeleteEntry(entry.Id.Value);
            _lastDeletedEntry = entry;
            UndoDeleteButton.IsVisible = true;
            _viewModel?.Refresh();
            await AutoSyncAfterChangeAsync();
        }
        catch (Exception exception) when (exception is ValidationException or DatabaseException)
        {
            AppLog.Error("Could not delete entry.", exception);
            await MessageDialog.ShowAsync(this, LocalizationService.T("MessageCannotDeleteEntry"), exception.Message, false);
        }
    }

    private async void OnUndoDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || _lastDeletedEntry is null)
        {
            return;
        }

        try
        {
            var restored = _service.SaveEntry(_lastDeletedEntry with { Id = null });
            _lastDeletedEntry = null;
            UndoDeleteButton.IsVisible = false;
            _viewModel?.SetWeekForDate(restored.Date);
            await AutoSyncAfterChangeAsync();
        }
        catch (Exception exception) when (exception is ValidationException or DatabaseException)
        {
            AppLog.Error("Could not undo entry deletion.", exception);
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotUndoDelete"),
                exception.Message,
                false);
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
        await AutoSyncAfterChangeAsync();
    }

    private async void OnEntryTypesClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new EntryTypesDialog(_service);
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
            var dropboxWasEnabled = _session.Settings.DropboxAutoSync;
            var service = _session.SwitchDatabase(result.DatabasePath, result.CopyCurrentData);
            RebindService(service);
            InitializeDropboxStatus();
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageDatabaseLocationChanged"),
                LocalizationService.T("MessageNowUsing", _session.Database.Path),
                false);
            if (dropboxWasEnabled)
            {
                await MessageDialog.ShowAsync(
                    this,
                    LocalizationService.T("MessageDatabaseLocationChanged"),
                    LocalizationService.T("MessageDropboxDisabledForNewDatabase"),
                    false);
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not change database location.", exception);
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotChangeDatabase"),
                exception.Message,
                false);
        }
    }

    private async void OnExportExcelClick(object? sender, RoutedEventArgs e) =>
        await OpenExportDialogAsync();

    private async Task OpenExportDialogAsync()
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new ExportDialog(_service);
        await dialog.ShowDialog(this);
    }

    private async void OnBackupClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.T("MenuBackup"),
            SuggestedFileName = $"TimeReport-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db",
            DefaultExtension = "db",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(LocalizationService.T("FileTypeSqliteDatabase"))
                {
                    Patterns = ["*.db"],
                },
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
                LocalizationService.T("MessageBackupComplete"),
                LocalizationService.T("MessageBackupSaved", saved),
                false);
        }
        catch (DatabaseException exception)
        {
            AppLog.Error("Could not create backup.", exception);
            await MessageDialog.ShowAsync(this, LocalizationService.T("MessageBackupFailed"), exception.Message, false);
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
            Title = LocalizationService.T("MenuRestore"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(LocalizationService.T("FileTypeSqliteDatabase"))
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
                LocalizationService.T("MessageRestoreTitle"),
                LocalizationService.T("MessageRestoreConfirm"),
                true))
        {
            return;
        }

        try
        {
            var autoBackup = _service.Database.RestoreFrom(files[0].Path.LocalPath);
            _viewModel?.Refresh();
            await AutoSyncAfterChangeAsync();
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageRestoreComplete"),
                LocalizationService.T("MessageRestoreDone", autoBackup),
                false);
        }
        catch (DatabaseException exception)
        {
            AppLog.Error("Could not restore database.", exception);
            await MessageDialog.ShowAsync(this, LocalizationService.T("MessageRestoreFailed"), exception.Message, false);
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => _viewModel?.Refresh();

    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string theme })
        {
            return;
        }

        AppThemes.Apply(theme);
        if (_session is not null)
        {
            _session.Settings.Theme = theme;
            try
            {
                _session.Settings.Save(_session.SettingsPath);
            }
            catch (Exception exception)
            {
                AppLog.Error("Could not save the theme setting.", exception);
            }
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var dialog = new AboutDialog(
            _service,
            _session?.DataDirectory ?? string.Empty,
            _session?.SettingsPath ?? string.Empty,
            AppLog.CurrentPath ?? string.Empty);
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
    }
}
