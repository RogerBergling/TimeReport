using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class ExportDialog : Window
{
    private readonly TimeReportService? _service;
    private readonly ExcelExportService? _exporter;

    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(TimeReportService service) : this()
    {
        _service = service;
        _exporter = new ExcelExportService(service);
        var (start, end) = TimeUtil.MonthBounds(DateOnly.FromDateTime(DateTime.Today));
        SetRange(start, end);
    }

    private void OnThisMonthClick(object? sender, RoutedEventArgs e)
    {
        var (start, end) = TimeUtil.MonthBounds(DateOnly.FromDateTime(DateTime.Today));
        SetRange(start, end);
    }

    private void OnLastMonthClick(object? sender, RoutedEventArgs e)
    {
        var previous = TimeUtil.AddMonths(DateOnly.FromDateTime(DateTime.Today), -1);
        var (start, end) = TimeUtil.MonthBounds(previous);
        SetRange(start, end);
    }

    private void OnThisYearClick(object? sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        SetRange(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31));
    }

    private void OnLastYearClick(object? sender, RoutedEventArgs e)
    {
        var year = DateTime.Today.Year - 1;
        SetRange(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
    }

    private async void OnAllTimeClick(object? sender, RoutedEventArgs e)
    {
        var bounds = _service?.AllTimeBounds();
        if (bounds is null)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("ExportDialogTitle"),
                LocalizationService.T("ExportNoData"),
                false);
            return;
        }
        SetRange(bounds.Value.Start, bounds.Value.End);
    }

    private void OnDateChanged(object? sender, TextChangedEventArgs e) => UpdateInfo();

    private void SetRange(DateOnly start, DateOnly end)
    {
        FromTextBox.Text = start.ToString("yyyy-MM-dd");
        ToTextBox.Text = end.ToString("yyyy-MM-dd");
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (InfoText is null)
        {
            return;
        }

        try
        {
            var start = TimeUtil.ParseDate(FromTextBox.Text ?? string.Empty);
            var end = TimeUtil.ParseDate(ToTextBox.Text ?? string.Empty);
            if (end < start)
            {
                InfoText.Text = LocalizationService.T("ExportInvalidRange");
                return;
            }
            InfoText.Text = LocalizationService.T(
                "ExportRangeInfo",
                start.ToString("yyyy-MM-dd"),
                end.ToString("yyyy-MM-dd"),
                (end.DayNumber - start.DayNumber) + 1);
        }
        catch (ValidationException exception)
        {
            InfoText.Text = exception.Message;
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_exporter is null)
        {
            return;
        }

        DateOnly start;
        DateOnly end;
        try
        {
            start = TimeUtil.ParseDate(FromTextBox.Text ?? string.Empty);
            end = TimeUtil.ParseDate(ToTextBox.Text ?? string.Empty);
        }
        catch (ValidationException exception)
        {
            InfoText.Text = exception.Message;
            return;
        }

        if (end < start)
        {
            InfoText.Text = LocalizationService.T("ExportInvalidRange");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.T("ExportDialogTitle"),
            SuggestedFileName = $"TimeReport-{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx",
            DefaultExtension = "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(LocalizationService.T("ExportFileType"))
                {
                    Patterns = ["*.xlsx"],
                },
            ],
        });

        if (file is null)
        {
            return;
        }

        InfoText.Text = LocalizationService.T("Exporting");
        try
        {
            var saved = await _exporter.ExportAsync(start, end, file.Path.LocalPath);
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("ExportSuccess"),
                LocalizationService.T("ExportSaved", saved),
                false);
            Close(true);
        }
        catch (Exception exception)
        {
            await MessageDialog.ShowAsync(
                this,
                LocalizationService.T("ExportFailed"),
                exception.Message,
                false);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
