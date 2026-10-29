using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class AboutDialog : Window
{
    private readonly string _dataDir = string.Empty;

    public AboutDialog()
    {
        InitializeComponent();
    }

    public AboutDialog(TimeReportService service, string dataDir) : this()
    {
        _dataDir = dataDir;
        VersionText.Text = $"Version {AppInfo.Version}";
        DatabaseText.Text = service.Database.Path;
        DataFolderText.Text = dataDir;
        SchemaText.Text = $"Schema version: {service.Database.SchemaVersionNumber}";
    }

    private async void OnOpenDataFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var executable = OperatingSystem.IsWindows()
                ? "explorer.exe"
                : OperatingSystem.IsMacOS()
                    ? "open"
                    : "xdg-open";
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = true };
            startInfo.ArgumentList.Add(_dataDir);
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            await MessageDialog.ShowAsync(this, "Cannot open data folder", exception.Message, false);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
