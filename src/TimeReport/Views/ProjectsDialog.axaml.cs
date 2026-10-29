using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class ProjectsDialog : Window
{
    private readonly TimeReportService? _service;

    public ProjectsDialog()
    {
        InitializeComponent();
    }

    public ProjectsDialog(TimeReportService service) : this()
    {
        _service = service;
        LoadProjects();
    }

    private void LoadProjects()
    {
        if (_service is null)
        {
            return;
        }

        var selectedId = (ProjectsList.SelectedItem as ProjectRow)?.Project.Id;
        var rows = _service.ListProjects(includeArchived: true)
            .Select(project => new ProjectRow(
                project,
                project.Archived
                    ? LocalizationService.T("ProjectStatusArchived")
                    : LocalizationService.T("ProjectStatusActive"),
                _service.ProjectEntryCount(project.Id)))
            .ToList();
        ProjectsList.ItemsSource = rows;
        if (selectedId is long id)
        {
            ProjectsList.SelectedItem = rows.FirstOrDefault(row => row.Project.Id == id);
        }
        else if (rows.Count > 0)
        {
            ProjectsList.SelectedItem = rows[0];
        }
        UpdateArchiveButton();
    }

    private ProjectRow? SelectedRow => ProjectsList.SelectedItem as ProjectRow;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UpdateArchiveButton();

    private void UpdateArchiveButton()
    {
        if (ArchiveButton is null)
        {
            return;
        }
        ArchiveButton.Content = SelectedRow?.Project.Archived == true
            ? LocalizationService.T("ButtonUnarchive")
            : LocalizationService.T("ButtonArchive");
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(
            this,
            LocalizationService.T("NewProjectTitle"),
            LocalizationService.T("ProjectNamePrompt"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _service.CreateProject(name);
            LoadProjects();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotCreateProject"),
                exception.Message,
                false);
        }
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not ProjectRow row)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(
            this,
            LocalizationService.T("RenameProjectTitle"),
            LocalizationService.T("ProjectNamePrompt"),
            row.Project.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _service.RenameProject(row.Project.Id, name);
            LoadProjects();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotRenameProject"),
                exception.Message,
                false);
        }
    }

    private async void OnArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not ProjectRow row)
        {
            return;
        }

        try
        {
            _service.SetProjectArchived(row.Project.Id, !row.Project.Archived);
            LoadProjects();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotChangeProject"),
                exception.Message,
                false);
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null || SelectedRow is not ProjectRow row)
        {
            return;
        }

        if (!await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageDeleteProjectTitle"),
                LocalizationService.T("MessageDeleteProject", row.Project.Name),
                true))
        {
            return;
        }

        try
        {
            _service.DeleteProject(row.Project.Id);
            LoadProjects();
        }
        catch (ValidationException exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("MessageCannotDeleteProject"),
                exception.Message,
                false);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

public sealed record ProjectRow(Project Project, string Status, int EntryCount)
{
    public string Name => Project.Name;
}
