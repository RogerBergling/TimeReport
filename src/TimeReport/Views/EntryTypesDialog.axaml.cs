using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class EntryTypesDialog : Window
{
    private readonly TimeReportService? _service;

    public EntryTypesDialog()
    {
        InitializeComponent();
    }

    public EntryTypesDialog(TimeReportService service) : this()
    {
        _service = service;
        LoadTypes();
    }

    private void LoadTypes()
    {
        if (_service is null)
        {
            return;
        }

        var selectedId = (TypesList.SelectedItem as EntryTypeRow)?.Type.Id;
        var rows = _service.ListEntryTypes(includeArchived: true)
            .Select(type => new EntryTypeRow(
                type,
                type.Category.DisplayName(),
                type.Archived
                    ? LocalizationService.T("ProjectStatusArchived")
                    : LocalizationService.T("ProjectStatusActive"),
                _service.EntryTypeUsageCount(type.Id)))
            .ToList();
        TypesList.ItemsSource = rows;
        if (selectedId is long id)
        {
            TypesList.SelectedItem = rows.FirstOrDefault(row => row.Type.Id == id);
        }
        else if (rows.Count > 0)
        {
            TypesList.SelectedItem = rows[0];
        }
        UpdateArchiveButton();
    }

    private EntryTypeRow? SelectedRow => TypesList.SelectedItem as EntryTypeRow;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UpdateArchiveButton();

    private void UpdateArchiveButton()
    {
        if (ArchiveButton is null)
        {
            return;
        }
        ArchiveButton.Content = SelectedRow?.Type.Archived == true
            ? LocalizationService.T("ButtonUnarchive")
            : LocalizationService.T("ButtonArchive");
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var draft = await EntryTypeEditDialog.ShowAsync(this);
        if (draft is null)
        {
            return;
        }

        try
        {
            _service.CreateEntryType(draft.Name, draft.Category);
            LoadTypes();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotCreateType"),
                exception.Message,
                false);
        }
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not EntryTypeRow row)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(
            this,
            LocalizationService.T("RenameTypeTitle"),
            LocalizationService.T("TypeNamePrompt"),
            row.Type.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _service.RenameEntryType(row.Type.Id, name);
            LoadTypes();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotRenameType"),
                exception.Message,
                false);
        }
    }

    private async void OnArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not EntryTypeRow row)
        {
            return;
        }

        try
        {
            _service.SetEntryTypeArchived(row.Type.Id, !row.Type.Archived);
            LoadTypes();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotChangeType"),
                exception.Message,
                false);
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not EntryTypeRow row)
        {
            return;
        }

        if (!await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageDeleteTypeTitle"),
                LocalizationService.T("MessageDeleteType", row.Type.Name),
                true))
        {
            return;
        }

        try
        {
            _service.DeleteEntryType(row.Type.Id);
            LoadTypes();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotDeleteType"),
                exception.Message,
                false);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

public sealed record EntryTypeRow(EntryType Type, string Category, string Status, int EntryCount)
{
    public string Name => Type.Name;
}
