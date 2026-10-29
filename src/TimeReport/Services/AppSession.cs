using TimeReport.Data;

namespace TimeReport.Services;

public sealed class AppSession : IDisposable
{
    public AppSession(
        string? databaseOverride = null,
        string? dataDirOverride = null,
        string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? AppPaths.SettingsPath();
        Settings = AppSettings.Load(SettingsPath);
        DefaultDatabasePath = AppPaths.DefaultDatabasePath();

        var initialPath = databaseOverride;
        if (string.IsNullOrWhiteSpace(initialPath) && !string.IsNullOrWhiteSpace(dataDirOverride))
        {
            initialPath = Path.Combine(dataDirOverride, "timereport.db");
        }
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            initialPath = Environment.GetEnvironmentVariable("TIMEREPORT_DATABASE");
        }
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            initialPath = Settings.DatabasePath;
        }
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            initialPath = DefaultDatabasePath;
        }

        OpenDatabase(Path.GetFullPath(initialPath));
    }

    public Database Database { get; private set; } = null!;

    public TimeReportService Service { get; private set; } = null!;

    public AppSettings Settings { get; }

    public string SettingsPath { get; }

    public string DefaultDatabasePath { get; }

    public string DataDirectory =>
        Path.GetDirectoryName(Database.Path)
        ?? throw new DatabaseException("The database path has no directory.");

    public TimeReportService SwitchDatabase(string newPath, bool copyCurrentData)
    {
        var fullPath = Path.GetFullPath(newPath);
        if (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, "timereport.db");
        }
        if (PathsEqual(fullPath, Database.Path))
        {
            return Service;
        }

        if (File.Exists(fullPath))
        {
            Database.ValidateFile(fullPath);
        }
        else
        {
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new DatabaseException("The new database path has no directory.");
            Directory.CreateDirectory(directory);
            if (copyCurrentData)
            {
                Database.BackupTo(fullPath);
            }
        }

        var previousDatabase = Database;
        previousDatabase.Close();
        try
        {
            var newDatabase = new Database(fullPath);
            newDatabase.Open();
            Database = newDatabase;
            Service = new TimeReportService(newDatabase);
            Settings.DatabasePath = fullPath;
            Settings.Save(SettingsPath);
            previousDatabase.Dispose();
            return Service;
        }
        catch
        {
            previousDatabase.Open();
            throw;
        }
    }

    public void Dispose() => Database.Dispose();

    private void OpenDatabase(string path)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new DatabaseException("The database path has no directory.");
        AppPaths.EnsureDataDir(directory);
        var database = new Database(path);
        database.Open();
        Database = database;
        Service = new TimeReportService(database);
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
