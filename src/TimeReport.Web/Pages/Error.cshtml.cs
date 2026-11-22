using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimeReport.Web.Pages;

[AllowAnonymous]
public sealed class ErrorModel : PageModel
{
    public int HttpStatus { get; private set; } = 500;

    public string Heading { get; private set; } = "Something went wrong";

    public string Message { get; private set; } =
        "The request could not be completed. Try again, or check the server log.";

    public void OnGet(int? code = null)
    {
        HttpStatus = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = HttpStatus;

        (Heading, Message) = HttpStatus switch
        {
            400 => ("Bad request", "The request was not valid. Go back and try again."),
            401 => ("Sign in required", "Your session has expired. Sign in again."),
            403 => ("Access denied", "Your account does not have permission for that page."),
            404 => ("Page not found", "That page does not exist. It may have moved or been deleted."),
            405 => ("Method not allowed", "That action is not available from this page."),
            _ => ("Something went wrong", "The request could not be completed. Try again, or check the server log."),
        };
    }
}
