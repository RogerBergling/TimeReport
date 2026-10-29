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
                project.Archived ? "Archived" : "Active",
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
        ArchiveButton.Content = SelectedRow?.Project.Archived == true ? "Unarchive" : "Archive";
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_service is null)
        {
            return;
        }

        var name = await TextInputDialog.ShowAsync(this, "New project", "Project name:");
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
            await MessageDialog.ShowAsync(this, "Cannot create project", exception.Message, false);
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
            "Rename project",
            "Project name:",
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
            await MessageDialog.ShowAsync(this, "Cannot rename project", exception.Message, false);
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
            await MessageDialog.ShowAsync(this, "Cannot change project", exception.Message, false);
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
                "Delete project",
                $"Delete \"{row.Project.Name}\"?\n\nProjects with time entries can only be archived.",
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
            await MessageDialog.ShowAsync(this, "Cannot delete project", exception.Message, false);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

public sealed record ProjectRow(Project Project, string Status, int EntryCount)
{
    public string Name => Project.Name;
}
