using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeReport.Services;

namespace TimeReport.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettings? _settings;

    public SettingsDialog()
    {
        InitializeComponent();
    }

    public SettingsDialog(AppSettings settings) : this()
    {
        _settings = settings;
        var timeZones = TimeZoneService.GetOptions();
        TimeZoneComboBox.ItemsSource = timeZones;
        TimeZoneComboBox.SelectedItem = FindTimeZone(
            timeZones,
            TimeZoneService.ResolveId(settings.TimeZoneId)) ?? (timeZones.Count > 0 ? timeZones[0] : null);

        var themes = new[]
        {
            new ThemeOption("system", LocalizationService.T("ThemeSystem")),
            new ThemeOption("light", LocalizationService.T("ThemeLight")),
            new ThemeOption("dark", LocalizationService.T("ThemeDark")),
            new ThemeOption("highcontrast", LocalizationService.T("ThemeHighContrast")),
        };
        ThemeComboBox.ItemsSource = themes;
        ThemeComboBox.SelectedItem = themes.FirstOrDefault(option =>
            string.Equals(option.Code, settings.Theme ?? "system", StringComparison.OrdinalIgnoreCase))
            ?? themes[0];

        DailyOvertimeTextBox.Text = FormatHours(settings.OvertimeDailyMinutes);
        WeeklyOvertimeTextBox.Text = FormatHours(settings.OvertimeWeeklyMinutes);
        BackupRetentionTextBox.Text = settings.BackupRetentionCount.ToString(CultureInfo.InvariantCulture);
        CheckForUpdatesCheckBox.IsChecked = settings.CheckForUpdates;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            Close(false);
            return;
        }

        if (!TryParseHours(DailyOvertimeTextBox.Text, 24, out var dailyMinutes) ||
            !TryParseHours(WeeklyOvertimeTextBox.Text, 168, out var weeklyMinutes))
        {
            ValidationText.Text = LocalizationService.T("SettingsInvalidOvertime");
            return;
        }
        if (!int.TryParse(
                BackupRetentionTextBox.Text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var retention) ||
            retention < 1 ||
            retention > 365)
        {
            ValidationText.Text = LocalizationService.T("SettingsInvalidRetention");
            return;
        }

        _settings.TimeZoneId = (TimeZoneComboBox.SelectedItem as TimeZoneOption)?.Id;
        _settings.Theme = (ThemeComboBox.SelectedItem as ThemeOption)?.Code ?? "system";
        _settings.OvertimeDailyMinutes = dailyMinutes;
        _settings.OvertimeWeeklyMinutes = weeklyMinutes;
        _settings.BackupRetentionCount = retention;
        _settings.CheckForUpdates = CheckForUpdatesCheckBox.IsChecked == true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private static TimeZoneOption? FindTimeZone(
        IReadOnlyList<TimeZoneOption> options,
        string id)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Id, id, StringComparison.Ordinal))
            {
                return option;
            }
        }
        return null;
    }

    private static string FormatHours(int minutes) =>
        (minutes / 60d).ToString("0.##", CultureInfo.CurrentCulture);

    private static bool TryParseHours(string? text, int maxHours, out int minutes)
    {
        minutes = 0;
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours) &&
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }
        if (hours < 0 || hours > maxHours)
        {
            return false;
        }
        minutes = (int)Math.Round(hours * 60);
        return true;
    }
}

public sealed record ThemeOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
