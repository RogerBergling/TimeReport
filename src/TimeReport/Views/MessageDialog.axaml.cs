using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TimeReport.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    private MessageDialog(string title, string message, bool confirm) : this()
    {
        Title = title;
        MessageText.Text = message;
        CancelButton.IsVisible = confirm;
        if (confirm)
        {
            OkButton.IsDefault = false;
            CancelButton.IsDefault = true;
        }
        Opened += (_, _) =>
        {
            if (confirm)
            {
                CancelButton.Focus();
            }
            else
            {
                OkButton.Focus();
            }
        };
    }

    public static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string message,
        bool confirm)
    {
        var dialog = new MessageDialog(title, message, confirm);
        return await dialog.ShowDialog<bool>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
