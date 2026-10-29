using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TimeReport.Data;

namespace TimeReport.Views;

public partial class DatabaseLocationDialog : Window
{
    private readonly string _currentPath;
    private readonly string _defaultPath;

    public DatabaseLocationDialog()
    {
        InitializeComponent();
        _currentPath = string.Empty;
        _defaultPath = string.Empty;
        KeyDown += OnKeyDown;
    }

    public DatabaseLocationDialog(string currentPath, string defaultPath) : this()
    {
        _currentPath = currentPath;
        _defaultPath = defaultPath;
        CurrentPathText.Text = currentPath;
        PathTextBox.Text = currentPath;
        UpdateStatus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the database folder",
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }

        PathTextBox.Text = Path.Combine(folders[0].Path.LocalPath, "timereport.db");
    }

    private void OnPathChanged(object? sender, TextChangedEventArgs e) => UpdateStatus();

    private void OnUseDefaultClick(object? sender, RoutedEventArgs e) =>
        PathTextBox.Text = _defaultPath;

    private void UpdateStatus()
    {
        if (StatusText is null || SaveButton is null || CopyCurrentCheckBox is null)
        {
            return;
        }

        var path = NormalizePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "Enter a valid database path.";
            SaveButton.IsEnabled = false;
            CopyCurrentCheckBox.IsEnabled = false;
            return;
        }

        if (PathsEqual(path, _currentPath))
        {
            StatusText.Text = "This is the current database.";
            SaveButton.IsEnabled = false;
            CopyCurrentCheckBox.IsEnabled = false;
            return;
        }

        if (File.Exists(path))
        {
            try
            {
                Database.ValidateFile(path);
                StatusText.Text = "An existing TimeReport database will be opened. The current database file is left untouched.";
                SaveButton.IsEnabled = true;
            }
            catch (DatabaseException exception)
            {
                StatusText.Text = $"This file cannot be used: {exception.Message}";
                SaveButton.IsEnabled = false;
            }
            CopyCurrentCheckBox.IsEnabled = false;
            return;
        }

        StatusText.Text = "A new database will be created at this location.";
        SaveButton.IsEnabled = true;
        CopyCurrentCheckBox.IsEnabled = true;
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var path = NormalizePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            await MessageDialog.ShowAsync(
                this,
                "Cannot use this database",
                "Enter a valid database path.",
                false);
            return;
        }

        if (PathsEqual(path, _currentPath))
        {
            Close(null);
            return;
        }

        if (File.Exists(path))
        {
            try
            {
                Database.ValidateFile(path);
            }
            catch (DatabaseException exception)
            {
                await MessageDialog.ShowAsync(
                    this,
                    "Cannot use this database",
                    exception.Message,
                    false);
                return;
            }
        }

        Close(new DatabaseLocationResult(
            path,
            CopyCurrentCheckBox.IsChecked == true && !File.Exists(path)));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private string NormalizePath()
    {
        try
        {
            var text = PathTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = ExpandHome(Environment.ExpandEnvironmentVariables(text));
            return Directory.Exists(text)
                ? Path.Combine(text, "timereport.db")
                : Path.GetFullPath(text);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }
        return path;
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

public sealed record DatabaseLocationResult(string DatabasePath, bool CopyCurrentData);
