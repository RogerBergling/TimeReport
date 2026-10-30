using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class EntryTypeEditDialog : Window
{
    public EntryTypeEditDialog()
    {
        InitializeComponent();
        CategoryComboBox.ItemsSource = Enum.GetValues<EntryKind>()
            .Select(category => new EntryTypeCategoryOption(category, category.DisplayName()))
            .ToList();
        CategoryComboBox.SelectedIndex = 0;
    }

    public static async Task<EntryTypeDraft?> ShowAsync(Window owner)
    {
        var dialog = new EntryTypeEditDialog();
        return await dialog.ShowDialog<EntryTypeDraft?>(owner);
    }

    private EntryTypeCategoryOption? SelectedCategory =>
        CategoryComboBox.SelectedItem as EntryTypeCategoryOption;

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = LocalizationService.T("ValidationTypeNameEmpty");
            return;
        }
        if (name.Length > 80)
        {
            ErrorText.Text = LocalizationService.T("ValidationTypeNameLength");
            return;
        }
        if (SelectedCategory is null)
        {
            ErrorText.Text = LocalizationService.T("ValidationTypeCategory");
            return;
        }

        Close(new EntryTypeDraft(name, SelectedCategory.Category));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}

public sealed record EntryTypeDraft(string Name, EntryKind Category);

public sealed record EntryTypeCategoryOption(EntryKind Category, string Label)
{
    public override string ToString() => Label;
}
