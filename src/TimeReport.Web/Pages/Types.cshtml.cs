using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages;

[Authorize]
public sealed class TypesModel : AppPageModel
{
    private readonly UserDataStore _data;

    public TypesModel(UserDataStore data)
    {
        _data = data;
    }

    public IReadOnlyList<EntryTypeRowView> Types { get; private set; } = [];

    public IReadOnlyList<EntryKind> Categories { get; } =
    [
        EntryKind.Work,
        EntryKind.Lunch,
        EntryKind.Away,
        EntryKind.XPause,
    ];

    public void OnGet() => Load();

    public IActionResult OnPostCreate(string? name, string? category)
    {
        try
        {
            var kind = EntryKindExtensions.FromDbString(category ?? string.Empty);
            _data.Current.Service.CreateEntryType(name ?? string.Empty, kind);
            SetStatus("Entry type created.");
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (ArgumentException)
        {
            SetError("Choose a valid type category.");
        }
        return RedirectToPage();
    }

    public IActionResult OnPostRename(long id, string? name)
    {
        try
        {
            _data.Current.Service.RenameEntryType(id, name ?? string.Empty);
            SetStatus("Entry type renamed.");
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
            _data.Current.Service.SetEntryTypeArchived(id, archived);
            SetStatus(archived ? "Entry type archived." : "Entry type restored.");
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
            _data.Current.Service.DeleteEntryType(id);
            SetStatus("Entry type deleted.");
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
        Types = service.ListEntryTypes(includeArchived: true)
            .Select(type => new EntryTypeRowView(type, service.EntryTypeUsageCount(type.Id)))
            .ToList();
    }
}
