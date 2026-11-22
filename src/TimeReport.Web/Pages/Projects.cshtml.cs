using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Data;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[Authorize]
public sealed class ProjectsModel : AppPageModel
{
    private readonly UserDataStore _data;

    public ProjectsModel(UserDataStore data)
    {
        _data = data;
    }

    public IReadOnlyList<ProjectRowView> Projects { get; private set; } = [];

    public void OnGet() => Load();

    public IActionResult OnPostCreate(string? name)
    {
        try
        {
            _data.Current.Service.CreateProject(name ?? string.Empty);
            SetStatus("Project created.");
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (DatabaseException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostRename(long id, string? name)
    {
        try
        {
            _data.Current.Service.RenameProject(id, name ?? string.Empty);
            SetStatus("Project renamed.");
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostArchive(long id, bool archived)
    {
        try
        {
            _data.Current.Service.SetProjectArchived(id, archived);
            SetStatus(archived ? "Project archived." : "Project restored.");
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(long id)
    {
        try
        {
            _data.Current.Service.DeleteProject(id);
            SetStatus("Project deleted.");
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        return RedirectToPage();
    }

    private void Load()
    {
        var service = _data.Current.Service;
        Projects = service.ListProjects(includeArchived: true)
            .Select(project => new ProjectRowView(project, service.ProjectEntryCount(project.Id)))
            .ToList();
    }
}
