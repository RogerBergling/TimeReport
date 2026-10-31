using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class AboutDialog : Window
{
    private readonly string _dataDir = string.Empty;
    private readonly string _logPath = string.Empty;

    public AboutDialog()
    {
        InitializeComponent();
    }

    public AboutDialog(
        TimeReportService service,
        string dataDir,
        string settingsPath,
        string logPath) : this()
    {
        _dataDir = dataDir;
        _logPath = logPath;
        VersionText.Text = $"{LocalizationService.T("VersionLabel")} {AppInfo.Version}";
        DatabaseText.Text = service.Database.Path;
        DataFolderText.Text = dataDir;
        SettingsText.Text = settingsPath;
        LogFileText.Text = logPath;
        SchemaText.Text = LocalizationService.T(
            "AboutSchemaVersion",
            service.Database.SchemaVersionNumber);
    }

    private async void OnOpenDataFolderClick(object? sender, RoutedEventArgs e) =>
        await OpenPathAsync(_dataDir, "MessageCannotOpenDataFolder");

    private async void OnOpenLogFileClick(object? sender, RoutedEventArgs e) =>
        await OpenPathAsync(_logPath, "MessageCannotOpenLogFile");

    private async Task OpenPathAsync(string path, string errorKey)
    {
        try
        {
            var executable = OperatingSystem.IsWindows()
                ? "explorer.exe"
                : OperatingSystem.IsMacOS()
                    ? "open"
                    : "xdg-open";
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = true };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T(errorKey),
                exception.Message,
                false);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
