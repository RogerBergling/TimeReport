using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class DropboxSyncDialog : Window
{
    private const string DefaultRemotePath = "/TimeReport/timereport.db";

    private readonly AppSession? _session;
    private readonly IDropboxSyncService? _dropbox;
    private readonly DropboxTokenProvider? _tokenProvider;

    public DropboxSyncDialog()
    {
        InitializeComponent();
    }

    public DropboxSyncDialog(
        AppSession session,
        IDropboxSyncService dropbox,
        DropboxTokenProvider tokenProvider) : this()
    {
        _session = session;
        _dropbox = dropbox;
        _tokenProvider = tokenProvider;
        AppKeyTextBox.Text = session.Settings.DropboxAppKey ?? string.Empty;
        AppSecretTextBox.Text = session.Settings.DropboxAppSecret ?? string.Empty;
        RemotePathTextBox.Text = string.IsNullOrWhiteSpace(session.Settings.DropboxRemotePath)
            ? DefaultRemotePath
            : session.Settings.DropboxRemotePath;
        AutoSyncCheckBox.IsChecked = session.Settings.DropboxAutoSync;
        if (tokenProvider.IsConnected)
        {
            StatusText.Text = LocalizationService.T("DropboxConnected");
        }
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (_tokenProvider is null)
        {
            return;
        }

        var appKey = AppKeyTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(appKey))
        {
            StatusText.Text = LocalizationService.T("DropboxAppKeyRequired");
            return;
        }

        StatusText.Text = LocalizationService.T("DropboxConnecting");
        try
        {
            await _tokenProvider.ConnectAsync(appKey, AppSecretTextBox.Text?.Trim());
            StatusText.Text = LocalizationService.T("DropboxConnected");
        }
        catch (Exception exception)
        {
            StatusText.Text = LocalizationService.T("DropboxAuthFailed", exception.Message);
        }
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        _tokenProvider?.Disconnect();
        StatusText.Text = LocalizationService.T("DropboxDisconnected");
    }

    private async void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (_dropbox is null || _tokenProvider is null)
        {
            return;
        }

        StatusText.Text = LocalizationService.T("DropboxTesting");
        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync(forceRefresh: true);
            await _dropbox.TestConnectionAsync(token);
            StatusText.Text = LocalizationService.T("DropboxConnected");
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private void OnOpenAppConsoleClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.dropbox.com/developers/apps")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            _ = MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotOpenDropbox"),
                exception.Message,
                false);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var appKey = AppKeyTextBox.Text?.Trim() ?? string.Empty;
        var appSecret = AppSecretTextBox.Text?.Trim();
        var remotePath = NormalizeRemotePath(RemotePathTextBox.Text);
        if (remotePath is null)
        {
            StatusText.Text = LocalizationService.T("DropboxPathInvalid");
            return;
        }

        var settings = _session.Settings;
        var credentialsChanged =
            !string.Equals(settings.DropboxAppKey, appKey, StringComparison.Ordinal) ||
            !string.Equals(settings.DropboxAppSecret, appSecret, StringComparison.Ordinal);
        if (credentialsChanged)
        {
            settings.DropboxRefreshToken = null;
            settings.DropboxAccessToken = null;
            settings.DropboxAccessTokenExpiresUtc = null;
            settings.DropboxLastSyncUtc = null;
        }

        if (!string.Equals(settings.DropboxRemotePath, remotePath, StringComparison.Ordinal))
        {
            settings.DropboxLastSyncUtc = null;
        }

        settings.DropboxAppKey = string.IsNullOrWhiteSpace(appKey) ? null : appKey;
        settings.DropboxAppSecret = string.IsNullOrWhiteSpace(appSecret) ? null : appSecret;
        settings.DropboxRemotePath = remotePath;
        settings.DropboxAutoSync = AutoSyncCheckBox.IsChecked == true;
        settings.Save(_session.SettingsPath);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private static string? NormalizeRemotePath(string? value)
    {
        var path = value?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return DefaultRemotePath;
        }
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }
        return path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
