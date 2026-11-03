using System.Security.Cryptography;
using TimeReport.Data;
using TimeReport.Models;

namespace TimeReport.Services;

public sealed class AppSession : IDisposable
{
    private readonly SingleInstanceGuard _instanceGuard;

    public AppSession(
        string? databaseOverride = null,
        string? dataDirOverride = null,
        string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? AppPaths.SettingsPath();
        var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(SettingsPath))
            ?? throw new DatabaseException("The settings path has no directory.");
        _instanceGuard = SingleInstanceGuard.Acquire(
            Path.Combine(settingsDirectory, "timereport.lock"));
        try
        {
            Settings = AppSettings.Load(SettingsPath);
        }
        catch
        {
            _instanceGuard.Dispose();
            throw;
        }

        DefaultDatabasePath = AppPaths.DefaultDatabasePath();

        var initialPath = databaseOverride;
        var hardenDatabaseDirectory = false;
        if (string.IsNullOrWhiteSpace(initialPath) && !string.IsNullOrWhiteSpace(dataDirOverride))
        {
            initialPath = AppPaths.DatabasePath(AppPaths.ResolveDataDir(dataDirOverride));
            hardenDatabaseDirectory = true;
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
            hardenDatabaseDirectory = true;
        }

        try
        {
            var fullInitialPath = Path.GetFullPath(AppPaths.ExpandHome(initialPath));
            if (!hardenDatabaseDirectory &&
                string.IsNullOrWhiteSpace(databaseOverride) &&
                string.IsNullOrWhiteSpace(dataDirOverride) &&
                PathsEqual(
                    Path.GetDirectoryName(fullInitialPath) ?? string.Empty,
                    AppPaths.ResolveDataDir()))
            {
                hardenDatabaseDirectory = true;
            }

            var keyGenerated = false;
            if (string.IsNullOrWhiteSpace(Settings.DatabaseEncryptionKey))
            {
                if (File.Exists(fullInitialPath) &&
                    new FileInfo(fullInitialPath).Length > 0 &&
                    !Database.IsPlaintextFile(fullInitialPath))
                {
                    throw new SettingsException(
                        "The database is encrypted, but settings.json does not contain its " +
                        "database encryption key. Restore settings.json and settings.json.key " +
                        "from backup before starting TimeReport.");
                }

                Settings.DatabaseEncryptionKey = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32));
                keyGenerated = true;
            }
            if (string.IsNullOrWhiteSpace(Settings.DropboxSyncKey))
            {
                Settings.DropboxSyncKey = SyncEncryptionService.GenerateKey();
                keyGenerated = true;
            }
            if (keyGenerated)
            {
                Settings.Save(SettingsPath);
            }

            OpenDatabase(fullInitialPath, hardenDatabaseDirectory);
        }
        catch
        {
            _instanceGuard.Dispose();
            throw;
        }
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
            Database.ValidateFile(fullPath, Settings.DatabaseEncryptionKey);
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
            var newDatabase = new Database(fullPath, Settings.DatabaseEncryptionKey);
            newDatabase.Open();
            Database = newDatabase;
            Service = new TimeReportService(newDatabase);
            ApplyServiceSettings(Service);
            Settings.DatabasePath = fullPath;
            // Never let an existing Dropbox remote silently receive a different
            // local database after a switch. The user can re-enable sync explicitly.
            Settings.DropboxAutoSync = false;
            Settings.DropboxLastSyncUtc = null;
            Settings.Save(SettingsPath);
            previousDatabase.Dispose();
            return Service;
        }
        catch
        {
            Database = previousDatabase;
            Service = new TimeReportService(previousDatabase);
            previousDatabase.Open();
            throw;
        }
    }

    public void Dispose()
    {
        Database.Dispose();
        _instanceGuard.Dispose();
    }

    public void ApplyRuntimeSettings() => ApplyServiceSettings(Service);

    private void ApplyServiceSettings(TimeReportService service)
    {
        service.TimeZoneId = TimeZoneService.ResolveId(Settings.TimeZoneId);
        service.OvertimePolicy = new OvertimePolicy(
            Math.Max(0, Settings.OvertimeDailyMinutes),
            Math.Max(0, Settings.OvertimeWeeklyMinutes));
        service.Database.BackupRetentionCount = Math.Max(1, Settings.BackupRetentionCount);
    }

    private void OpenDatabase(string path, bool hardenDirectory)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new DatabaseException("The database path has no directory.");
        if (hardenDirectory)
        {
            AppPaths.EnsureDataDir(directory);
        }
        else
        {
            AppPaths.EnsureDirectory(directory);
        }
        AppPaths.CleanupTemporaryFiles(directory);

        var database = new Database(path, Settings.DatabaseEncryptionKey);
        database.Open();
        Database = database;
        Service = new TimeReportService(database);
        ApplyServiceSettings(Service);
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
