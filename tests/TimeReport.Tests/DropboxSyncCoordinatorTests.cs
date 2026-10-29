using TimeReport.Models;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class DropboxSyncCoordinatorTests
{
    [Fact]
    public async Task NotConfiguredReturnsNotConfigured()
    {
        using var context = new SessionContext();
        var coordinator = new DropboxSyncCoordinator(context.Session, new FakeDropboxSyncService());

        var result = await coordinator.SyncNowAsync();

        Assert.Equal(DropboxSyncStatus.NotConfigured, result.Status);
    }

    [Fact]
    public async Task AutoSyncUploadsWhenRemoteFileIsMissing()
    {
        using var context = new SessionContext();
        context.Session.Service.CreateProject("Local");
        Configure(context.Session, lastSync: null);
        var dropbox = new FakeDropboxSyncService { RemoteMetadata = null };
        var coordinator = new DropboxSyncCoordinator(context.Session, dropbox);

        var result = await coordinator.AutoSyncAfterChangeAsync();

        Assert.Equal(DropboxSyncStatus.Uploaded, result.Status);
        Assert.Equal(1, dropbox.UploadCount);
        Assert.NotNull(context.Session.Settings.DropboxLastSyncUtc);
    }

    [Fact]
    public async Task AutoSyncRefusesToOverwriteRemoteChanges()
    {
        using var context = new SessionContext();
        context.Session.Service.CreateProject("Local");
        Configure(context.Session, lastSync: DateTimeOffset.UtcNow.AddHours(-1));
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow, "remote", null),
        };
        var coordinator = new DropboxSyncCoordinator(context.Session, dropbox);

        var result = await coordinator.AutoSyncAfterChangeAsync();

        Assert.Equal(DropboxSyncStatus.Conflict, result.Status);
        Assert.Equal(0, dropbox.UploadCount);
    }

    [Fact]
    public async Task FirstSyncDownloadsRemoteDatabaseWhenLocalIsEmpty()
    {
        using var source = new SessionContext();
        source.Session.Service.CreateProject("Remote");
        var remoteBytes = await File.ReadAllBytesAsync(source.Session.Database.Path);

        using var target = new SessionContext();
        Configure(target.Session, lastSync: null);
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow, "remote", null),
            RemoteContent = remoteBytes,
        };
        var coordinator = new DropboxSyncCoordinator(target.Session, dropbox);

        var result = await coordinator.SyncNowAsync();

        Assert.Equal(DropboxSyncStatus.Downloaded, result.Status);
        Assert.Equal(["Remote"], target.Session.Service.ListProjects().Select(project => project.Name));
        Assert.NotNull(target.Session.Settings.DropboxLastSyncUtc);
        Assert.NotNull(result.AutoBackupPath);
    }

    [Fact]
    public async Task StartupDownloadsNewerRemoteWhenLocalIsUnchanged()
    {
        using var source = new SessionContext();
        source.Session.Service.CreateProject("Remote");
        var remoteBytes = await File.ReadAllBytesAsync(source.Session.Database.Path);

        using var target = new SessionContext();
        Configure(target.Session, lastSync: DateTimeOffset.UtcNow);
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow.AddMinutes(1), "remote", null),
            RemoteContent = remoteBytes,
        };
        var coordinator = new DropboxSyncCoordinator(target.Session, dropbox);

        var result = await coordinator.AutoSyncOnStartupAsync();

        Assert.Equal(DropboxSyncStatus.Downloaded, result.Status);
        Assert.Equal(["Remote"], target.Session.Service.ListProjects().Select(project => project.Name));
    }

    [Fact]
    public async Task SyncNowUploadsLocalChangesWhenRemoteIsUnchanged()
    {
        using var context = new SessionContext();
        context.Session.Service.CreateProject("Local");
        Configure(context.Session, lastSync: DateTimeOffset.UtcNow.AddHours(-1));
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow.AddHours(-2), "remote", null),
        };
        var coordinator = new DropboxSyncCoordinator(context.Session, dropbox);

        var result = await coordinator.SyncNowAsync();

        Assert.Equal(DropboxSyncStatus.Uploaded, result.Status);
        Assert.Equal(1, dropbox.UploadCount);
    }

    [Fact]
    public async Task ConflictCanBeResolvedInFavorOfLocal()
    {
        using var context = new SessionContext();
        context.Session.Service.CreateProject("Local");
        Configure(context.Session, lastSync: DateTimeOffset.UtcNow.AddHours(-1));
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow, "remote", null),
        };
        var coordinator = new DropboxSyncCoordinator(context.Session, dropbox);

        var result = await coordinator.SyncNowAsync(
            () => Task.FromResult(DropboxConflictChoice.UseLocal));

        Assert.Equal(DropboxSyncStatus.Uploaded, result.Status);
        Assert.Equal(1, dropbox.UploadCount);
    }

    [Fact]
    public async Task UpToDateWhenNeitherSideChanged()
    {
        using var context = new SessionContext();
        var lastSync = DateTimeOffset.UtcNow;
        Configure(context.Session, lastSync);
        var dropbox = new FakeDropboxSyncService
        {
            RemoteMetadata = new DropboxMetadata(lastSync, "remote", null),
        };
        var coordinator = new DropboxSyncCoordinator(context.Session, dropbox);

        var result = await coordinator.SyncNowAsync();

        Assert.Equal(DropboxSyncStatus.UpToDate, result.Status);
        Assert.Equal(0, dropbox.UploadCount);
        Assert.Equal(0, dropbox.DownloadCount);
    }

    private static void Configure(AppSession session, DateTimeOffset? lastSync)
    {
        session.Settings.DropboxAccessToken = "token";
        session.Settings.DropboxRemotePath = "/TimeReport/timereport.db";
        session.Settings.DropboxAutoSync = true;
        session.Settings.DropboxLastSyncUtc = lastSync;
        session.Settings.Save(session.SettingsPath);
    }

    private sealed class SessionContext : IDisposable
    {
        public SessionContext()
        {
            Directory = Path.Combine(
                Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Session = new AppSession(
                databaseOverride: Path.Combine(Directory, "timereport.db"),
                settingsPath: Path.Combine(Directory, "settings.json"));
        }

        public string Directory { get; }

        public AppSession Session { get; }

        public void Dispose()
        {
            Session.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, true);
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
