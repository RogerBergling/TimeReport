using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Web.Auth;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Account;

[Authorize]
public sealed class SecurityModel : AppPageModel
{
    private readonly AuthDatabase _database;

    public SecurityModel(AuthDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<AuthAuditEntry> Events { get; private set; } = [];

    public void OnGet()
    {
        Events = _database.ListAudit(100)
            .Where(entry => string.Equals(entry.Actor, CurrentUser.Username, StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .ToList();
    }
}
