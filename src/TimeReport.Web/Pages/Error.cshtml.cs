using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Web.Localization;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class ErrorModel : PageModel
{
    public int HttpStatus { get; private set; } = 500;

    public string Heading { get; private set; } = WebText.T("Something went wrong");

    public string Message { get; private set; } =
        WebText.T("The request could not be completed. Try again, or check the server log.");

    public void OnGet(int? code = null)
    {
        HttpStatus = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = HttpStatus;

        (Heading, Message) = HttpStatus switch
        {
            400 => (WebText.T("Bad request"), WebText.T("The request was not valid. Go back and try again.")),
            401 => (WebText.T("Sign in required"), WebText.T("Your session has expired. Sign in again.")),
            403 => (WebText.T("Access denied"), WebText.T("Your account does not have permission for that page.")),
            404 => (WebText.T("Page not found"), WebText.T("That page does not exist. It may have moved or been deleted.")),
            405 => (WebText.T("Method not allowed"), WebText.T("That action is not available from this page.")),
            _ => (WebText.T("Something went wrong"), WebText.T("The request could not be completed. Try again, or check the server log.")),
        };
    }
}
