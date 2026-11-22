using Microsoft.AspNetCore.Authorization;
using TimeReport.Web.Auth;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class AuditModel : AppPageModel
{
    private readonly AuthDatabase _database;

    public AuditModel(AuthDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<AuthAuditEntry> Entries { get; private set; } = [];

    public void OnGet() => Entries = _database.ListAudit(300);
}
