using System.Security.Cryptography;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class AppSessionTests
{
    [Fact]
    public void SecondSessionForSameSettingsIsRejected()
    {
        var root = NewTempDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            using var first = new AppSession(
                databaseOverride: Path.Combine(root, "first.db"),
                settingsPath: settingsPath);

            Assert.Throws<SingleInstanceException>(() =>
                new AppSession(
                    databaseOverride: Path.Combine(root, "second.db"),
                    settingsPath: settingsPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SwitchDatabaseCopiesDataAndPersistsSettings()
    {
        var root = NewTempDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var originalPath = Path.Combine(root, "original.db");
            var newPath = Path.Combine(root, "new", "timereport.db");

            var session = new AppSession(
                databaseOverride: originalPath,
                settingsPath: settingsPath);
            try
            {
                var project = session.Service.CreateProject("Acme");
                session.Service.SaveEntry(TestData.Entry(
                    TestData.Monday,
                    EntryKind.Work,
                    "08:00",
                    "16:00",
                    project.Id));

                var newService = session.SwitchDatabase(newPath, copyCurrentData: true);

                Assert.True(File.Exists(originalPath));
                Assert.True(File.Exists(newPath));
                Assert.Equal(["Acme"], newService.ListProjects().Select(item => item.Name));
                Assert.Single(newService.ListEntries(TestData.Monday, TestData.Monday));
                Assert.Equal(Path.GetFullPath(newPath), AppSettings.Load(settingsPath).DatabasePath);
            }
            finally
            {
                session.Dispose();
            }

            using var reopened = new AppSession(settingsPath: settingsPath);
            Assert.Equal(["Acme"], reopened.Service.ListProjects().Select(item => item.Name));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SwitchToExistingDatabaseOpensExistingData()
    {
        var root = NewTempDirectory();
        try
        {
            var firstPath = Path.Combine(root, "first.db");
            var secondPath = Path.Combine(root, "second.db");
            var settingsPath = Path.Combine(root, "settings.json");

            using (var first = new AppSession(databaseOverride: firstPath, settingsPath: settingsPath))
            {
                first.Service.CreateProject("First");
            }

            using var second = new AppSession(databaseOverride: secondPath, settingsPath: settingsPath);
            second.Service.CreateProject("Second");

            var service = second.SwitchDatabase(firstPath, copyCurrentData: false);
            Assert.Equal(["First"], service.ListProjects().Select(item => item.Name));
            Assert.Equal(Path.GetFullPath(firstPath), AppSettings.Load(settingsPath).DatabasePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SwitchToNewDatabaseWithoutCopyCreatesEmptyDatabase()
    {
        var root = NewTempDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.db");
            var newPath = Path.Combine(root, "empty.db");
            var settingsPath = Path.Combine(root, "settings.json");

            using var session = new AppSession(
                databaseOverride: originalPath,
                settingsPath: settingsPath);
            session.Service.CreateProject("Original");

            var service = session.SwitchDatabase(newPath, copyCurrentData: false);
            Assert.Empty(service.ListProjects());
            Assert.True(File.Exists(originalPath));
            Assert.True(File.Exists(newPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InvalidExistingDatabaseIsRejectedAndCurrentRemains()
    {
        var root = NewTempDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.db");
            var invalidPath = Path.Combine(root, "invalid.db");
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(invalidPath, "not a database");

            using var session = new AppSession(
                databaseOverride: originalPath,
                settingsPath: settingsPath);
            session.Service.CreateProject("Original");

            Assert.Throws<RestoreException>(() =>
                session.SwitchDatabase(invalidPath, copyCurrentData: false));
            Assert.Equal(["Original"], session.Service.ListProjects().Select(item => item.Name));
            Assert.Equal(Path.GetFullPath(originalPath), session.Database.Path);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SwitchDatabaseAcceptsDirectoryPath()
    {
        var root = NewTempDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.db");
            var folder = Path.Combine(root, "database-folder");
            Directory.CreateDirectory(folder);
            var settingsPath = Path.Combine(root, "settings.json");

            using var session = new AppSession(
                databaseOverride: originalPath,
                settingsPath: settingsPath);
            session.Service.CreateProject("Original");

            session.SwitchDatabase(folder, copyCurrentData: true);
            Assert.Equal(Path.Combine(folder, "timereport.db"), session.Database.Path);
            Assert.True(File.Exists(Path.Combine(folder, "timereport.db")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SwitchingDatabaseDisablesDropboxAutoSync()
    {
        var root = NewTempDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.db");
            var newPath = Path.Combine(root, "new.db");
            var settingsPath = Path.Combine(root, "settings.json");
            using var session = new AppSession(
                databaseOverride: originalPath,
                settingsPath: settingsPath);
            session.Settings.DropboxAccessToken = "token";
            session.Settings.DropboxAutoSync = true;
            session.Settings.DropboxLastSyncUtc = DateTimeOffset.UtcNow;
            session.Settings.Save(settingsPath);

            session.SwitchDatabase(newPath, copyCurrentData: false);

            var saved = AppSettings.Load(settingsPath);
            Assert.False(saved.DropboxAutoSync);
            Assert.Null(saved.DropboxLastSyncUtc);
            Assert.Equal("token", saved.DropboxAccessToken);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EncryptedDatabaseWithoutSettingsKeyIsRejected()
    {
        var root = NewTempDirectory();
        try
        {
            var databasePath = Path.Combine(root, "encrypted.db");
            var settingsPath = Path.Combine(root, "settings.json");
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            using (var database = new Database(databasePath, key))
            {
                database.Open();
            }

            new AppSettings { DatabasePath = databasePath }.Save(settingsPath);

            Assert.Throws<SettingsException>(() =>
                new AppSession(
                    databaseOverride: databasePath,
                    settingsPath: settingsPath));
            Assert.Null(AppSettings.Load(settingsPath).DatabaseEncryptionKey);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SettingsSaveIsAtomicAndLeavesNoTemporaryFile()
    {
        var root = NewTempDirectory();
        try
        {
            var path = Path.Combine(root, "settings.json");
            var settings = new AppSettings { Language = "sv" };
            settings.Save(path);

            Assert.True(File.Exists(path));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
            Assert.Equal("sv", AppSettings.Load(path).Language);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var root = NewTempDirectory();
        try
        {
            var path = Path.Combine(root, "settings.json");
            var settings = new AppSettings
            {
                DatabasePath = Path.Combine(root, "custom.db"),
                Language = "sv",
                Theme = "dark",
                TimeZoneId = "UTC",
                OvertimeDailyMinutes = 480,
                OvertimeWeeklyMinutes = 2400,
                BackupRetentionCount = 30,
                CheckForUpdates = false,
                DropboxProxyUrl = "http://proxy.example:8080",
                DropboxCaCertificatePath = "/tmp/ca.pem",
                DropboxAppKey = "app-key",
                DropboxAppSecret = "app-secret",
                DropboxRefreshToken = "refresh-token",
                DropboxAccessToken = "token",
                DropboxAccessTokenExpiresUtc = new DateTimeOffset(2026, 10, 29, 13, 0, 0, TimeSpan.Zero),
                DropboxRemotePath = "/TimeReport/timereport.db",
                DropboxAutoSync = true,
                DropboxLastSyncUtc = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
            };
            settings.Save(path);

            var loaded = AppSettings.Load(path);
            Assert.Equal(settings.DatabasePath, loaded.DatabasePath);
            Assert.Equal(settings.Language, loaded.Language);
            Assert.Equal(settings.Theme, loaded.Theme);
            Assert.Equal(settings.TimeZoneId, loaded.TimeZoneId);
            Assert.Equal(settings.OvertimeDailyMinutes, loaded.OvertimeDailyMinutes);
            Assert.Equal(settings.OvertimeWeeklyMinutes, loaded.OvertimeWeeklyMinutes);
            Assert.Equal(settings.BackupRetentionCount, loaded.BackupRetentionCount);
            Assert.Equal(settings.CheckForUpdates, loaded.CheckForUpdates);
            Assert.Equal(settings.DropboxProxyUrl, loaded.DropboxProxyUrl);
            Assert.Equal(settings.DropboxCaCertificatePath, loaded.DropboxCaCertificatePath);
            Assert.Equal(settings.DropboxAppKey, loaded.DropboxAppKey);
            Assert.Equal(settings.DropboxAppSecret, loaded.DropboxAppSecret);
            Assert.Equal(settings.DropboxRefreshToken, loaded.DropboxRefreshToken);
            Assert.Equal(settings.DropboxAccessToken, loaded.DropboxAccessToken);
            Assert.Equal(settings.DropboxAccessTokenExpiresUtc, loaded.DropboxAccessTokenExpiresUtc);
            Assert.Equal(settings.DropboxRemotePath, loaded.DropboxRemotePath);
            Assert.Equal(settings.DropboxAutoSync, loaded.DropboxAutoSync);
            Assert.Equal(settings.DropboxLastSyncUtc, loaded.DropboxLastSyncUtc);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
