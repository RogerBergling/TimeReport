using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TimeReport.Views;

public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private TextInputDialog(string title, string prompt, string initialValue) : this()
    {
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = initialValue;
        Opened += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public static async Task<string?> ShowAsync(
        Window owner,
        string title,
        string prompt,
        string initialValue = "")
    {
        var dialog = new TextInputDialog(title, prompt, initialValue);
        return await dialog.ShowDialog<string?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) =>
        Close(InputTextBox.Text?.Trim());

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(InputTextBox.Text?.Trim());
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }
}
