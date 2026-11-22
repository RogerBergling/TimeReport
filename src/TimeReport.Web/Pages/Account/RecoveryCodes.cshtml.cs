using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeReport.Web.Auth;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Account;

[Authorize]
public sealed class RecoveryCodesModel : AppPageModel
{
    private readonly AuthService _auth;

    public RecoveryCodesModel(AuthService auth)
    {
        _auth = auth;
    }

    [BindProperty]
    public string? CurrentPassword { get; set; }

    public IReadOnlyList<string>? NewCodes { get; private set; }

    public bool Required { get; private set; }

    public int Remaining => CurrentUser.UnusedRecoveryCodes;

    public void OnGet(bool required = false)
    {
        Required = required || CurrentUser.UnusedRecoveryCodes == 0;
    }

    public IActionResult OnPost()
    {
        Required = CurrentUser.UnusedRecoveryCodes == 0;
        if (!_auth.VerifyCurrentPassword(CurrentUser, CurrentPassword, "recovery-codes", ClientIp))
        {
            ErrorMessage = "The current password is not correct.";
            CurrentPassword = null;
            return Page();
        }

        try
        {
            NewCodes = _auth.RegenerateRecoveryCodes(CurrentUser);
            Required = false;
            SetStatus("New recovery codes generated. Save them now; they are shown only once.");
            return Page();
        }
        catch (AuthException exception)
        {
            ErrorMessage = exception.Message;
            CurrentPassword = null;
            return Page();
        }
    }
}
