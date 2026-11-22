using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeReport.Data;
using TimeReport.Services;
using TimeReport.Web.Services;
using TimeReport.Web.ViewModels;

namespace TimeReport.Web.Pages.Export;

[Authorize]
public sealed class DownloadModel : AppPageModel
{
    private readonly UserDataStore _data;

    public DownloadModel(UserDataStore data)
    {
        _data = data;
    }

    public async Task<IActionResult> OnGetAsync(string? start, string? end)
    {
        DateOnly startDate;
        DateOnly endDate;
        try
        {
            startDate = TimeUtil.ParseDate(start ?? string.Empty);
            endDate = TimeUtil.ParseDate(end ?? string.Empty);
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
            return RedirectToPage("/Export");
        }

        if (endDate < startDate)
        {
            SetError(LocalizationService.T("ValidationDateRange"));
            return RedirectToPage("/Export");
        }

        if (startDate.Year < 1900 || endDate.Year > 9999)
        {
            SetError(LocalizationService.T("ExportDateOutOfRange"));
            return RedirectToPage("/Export");
        }

        if (endDate.DayNumber - startDate.DayNumber > 3_660)
        {
            SetError(LocalizationService.T("ExportRangeTooLarge"));
            return RedirectToPage("/Export");
        }

        var temporary = Path.Combine(
            Path.GetTempPath(),
            $".timereport-export-{Guid.NewGuid():N}.xlsx");
        try
        {
            var service = _data.Current.Service;
            await new ExcelExportService(service).ExportAsync(startDate, endDate, temporary);
            var bytes = await System.IO.File.ReadAllBytesAsync(temporary);
            var fileName = $"TimeReport-{startDate:yyyy-MM-dd}-{endDate:yyyy-MM-dd}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
            return RedirectToPage("/Export");
        }
        catch (DatabaseException exception)
        {
            SetError(exception.Message);
            return RedirectToPage("/Export");
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(temporary))
                {
                    System.IO.File.Delete(temporary);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
