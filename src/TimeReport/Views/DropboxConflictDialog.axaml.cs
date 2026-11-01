using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class DropboxConflictDialog : Window
{
    public DropboxConflictDialog()
    {
        InitializeComponent();
    }

    public DropboxConflictDialog(
        string localPath,
        DateTimeOffset localModified,
        string remotePath,
        DropboxMetadata remote) : this()
    {
        LocalText.Text =
            $"{localPath}\n{LocalizationService.T("DropboxConflictModified", localModified.LocalDateTime)}";
        RemoteText.Text =
            $"{remotePath}\n{LocalizationService.T("DropboxConflictModified", remote.ServerModified.LocalDateTime)}";
    }

    public static async Task<DropboxConflictChoice> ShowAsync(
        Window owner,
        string localPath,
        DateTimeOffset localModified,
        string remotePath,
        DropboxMetadata remote)
    {
        var dialog = new DropboxConflictDialog(localPath, localModified, remotePath, remote);
        return await dialog.ShowDialog<DropboxConflictChoice>(owner);
    }

    private void OnLocalClick(object? sender, RoutedEventArgs e) =>
        Close(DropboxConflictChoice.UseLocal);

    private void OnRemoteClick(object? sender, RoutedEventArgs e) =>
        Close(DropboxConflictChoice.UseRemote);

    private void OnCancelClick(object? sender, RoutedEventArgs e) =>
        Close(DropboxConflictChoice.Cancel);
}
