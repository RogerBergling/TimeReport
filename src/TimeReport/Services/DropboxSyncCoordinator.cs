using TimeReport.Data;

namespace TimeReport.Services;

public enum DropboxSyncStatus
{
    NotConfigured,
    Disabled,
    Ready,
    Syncing,
    UpToDate,
    Uploaded,
    Downloaded,
    Conflict,
    Error,
}

public enum DropboxConflictChoice
{
    UseLocal,
    UseRemote,
    Cancel,
}

public sealed record DropboxSyncResult(
    DropboxSyncStatus Status,
    string Message,
    string? AutoBackupPath = null);

public sealed class DropboxSyncCoordinator
{
    private const string DefaultRemotePath = "/TimeReport/timereport.db";
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

    private readonly AppSession _session;
    private readonly IDropboxSyncService _dropbox;
    private readonly DropboxTokenProvider _tokenProvider;

    public DropboxSyncCoordinator(
        AppSession session,
        IDropboxSyncService dropbox,
        DropboxTokenProvider? tokenProvider = null)
    {
        _session = session;
        _dropbox = dropbox;
        _tokenProvider = tokenProvider ?? new DropboxTokenProvider(session);
    }

    public bool IsConfigured =>
        _tokenProvider.IsConnected &&
        !string.IsNullOrWhiteSpace(RemotePath);

    public bool AutoSyncEnabled => IsConfigured && _session.Settings.DropboxAutoSync;

    public string RemotePath
    {
        get
        {
            var path = _session.Settings.DropboxRemotePath?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                return DefaultRemotePath;
            }
            return path.StartsWith('/') ? path : "/" + path;
        }
    }

    public async Task<DropboxSyncResult> SyncNowAsync(
        Func<DropboxMetadata, Task<DropboxConflictChoice>>? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.NotConfigured,
                LocalizationService.T("DropboxNotConfigured"));
        }

        try
        {
            var metadata = await GetMetadataAsync(cancellationToken);
            var lastSync = _session.Settings.DropboxLastSyncUtc;
            var localModified = File.GetLastWriteTimeUtc(_session.Database.Path);
            var localChanged = lastSync is null
                ? HasLocalData()
                : localModified > lastSync.Value.UtcDateTime.Add(TimestampTolerance);
            var remoteChanged = metadata is not null &&
                                (lastSync is null ||
                                 metadata.ServerModified.UtcDateTime > lastSync.Value.UtcDateTime.Add(TimestampTolerance));

            if (metadata is null)
            {
                return await UploadAsync(null, cancellationToken);
            }

            if (lastSync is null)
            {
                return !HasLocalData()
                    ? await DownloadAsync(metadata, cancellationToken)
                    : await ResolveConflictAsync(metadata, conflictResolver, cancellationToken);
            }

            if (localChanged && remoteChanged)
            {
                return await ResolveConflictAsync(metadata, conflictResolver, cancellationToken);
            }
            if (localChanged)
            {
                return await UploadAsync(metadata, cancellationToken);
            }
            if (remoteChanged)
            {
                return await DownloadAsync(metadata, cancellationToken);
            }

            return new DropboxSyncResult(
                DropboxSyncStatus.UpToDate,
                LocalizationService.T("DropboxUpToDate"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Error,
                LocalizationService.T("DropboxError", exception.Message));
        }
    }

    public async Task<DropboxSyncResult> AutoSyncAfterChangeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.NotConfigured,
                LocalizationService.T("DropboxNotConfigured"));
        }
        if (!AutoSyncEnabled)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Disabled,
                LocalizationService.T("DropboxDisabled"));
        }

        try
        {
            var metadata = await GetMetadataAsync(cancellationToken);
            if (metadata is null)
            {
                return await UploadAsync(null, cancellationToken);
            }

            var lastSync = _session.Settings.DropboxLastSyncUtc;
            if (lastSync is null ||
                metadata.ServerModified.UtcDateTime > lastSync.Value.UtcDateTime.Add(TimestampTolerance))
            {
                return new DropboxSyncResult(
                    DropboxSyncStatus.Conflict,
                    LocalizationService.T("DropboxConflict"));
            }

            return await UploadAsync(metadata, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Error,
                LocalizationService.T("DropboxError", exception.Message));
        }
    }

    public async Task<DropboxSyncResult> AutoSyncOnStartupAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.NotConfigured,
                LocalizationService.T("DropboxNotConfigured"));
        }
        if (!AutoSyncEnabled)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Disabled,
                LocalizationService.T("DropboxDisabled"));
        }

        try
        {
            var metadata = await GetMetadataAsync(cancellationToken);
            if (metadata is null)
            {
                return await UploadAsync(null, cancellationToken);
            }

            var lastSync = _session.Settings.DropboxLastSyncUtc;
            if (lastSync is null)
            {
                return !HasLocalData()
                    ? await DownloadAsync(metadata, cancellationToken)
                    : new DropboxSyncResult(
                        DropboxSyncStatus.Conflict,
                        LocalizationService.T("DropboxConflict"));
            }

            var localModified = File.GetLastWriteTimeUtc(_session.Database.Path);
            var localChanged = localModified > lastSync.Value.UtcDateTime.Add(TimestampTolerance);
            var remoteChanged = metadata.ServerModified.UtcDateTime > lastSync.Value.UtcDateTime.Add(TimestampTolerance);

            if (localChanged && remoteChanged)
            {
                return new DropboxSyncResult(
                    DropboxSyncStatus.Conflict,
                    LocalizationService.T("DropboxConflict"));
            }
            if (remoteChanged)
            {
                return await DownloadAsync(metadata, cancellationToken);
            }
            if (localChanged)
            {
                return await UploadAsync(metadata, cancellationToken);
            }

            return new DropboxSyncResult(
                DropboxSyncStatus.UpToDate,
                LocalizationService.T("DropboxUpToDate"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Error,
                LocalizationService.T("DropboxError", exception.Message));
        }
    }

    private async Task<DropboxSyncResult> ResolveConflictAsync(
        DropboxMetadata metadata,
        Func<DropboxMetadata, Task<DropboxConflictChoice>>? conflictResolver,
        CancellationToken cancellationToken)
    {
        if (conflictResolver is null)
        {
            return new DropboxSyncResult(
                DropboxSyncStatus.Conflict,
                LocalizationService.T("DropboxConflict"));
        }

        var choice = await conflictResolver(metadata);
        return choice switch
        {
            DropboxConflictChoice.UseLocal => await UploadAsync(metadata, cancellationToken),
            DropboxConflictChoice.UseRemote => await DownloadAsync(metadata, cancellationToken),
            _ => new DropboxSyncResult(
                DropboxSyncStatus.Conflict,
                LocalizationService.T("DropboxConflict")),
        };
    }

    private async Task<DropboxSyncResult> UploadAsync(
        DropboxMetadata? expectedRemote,
        CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(
            _session.DataDirectory,
            $".dropbox-upload-{Guid.NewGuid():N}.db");
        var encrypted = temporary + ".enc";
        try
        {
            _session.Database.BackupToPlaintext(temporary);
            var uploadPath = temporary;
            if (_session.Settings.DropboxSyncEncryptionEnabled)
            {
                var key = _session.Settings.DropboxSyncKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new DropboxSyncException(
                        LocalizationService.T("DropboxSyncKeyMissing"));
                }
                SyncEncryptionService.EncryptFile(temporary, encrypted, key);
                uploadPath = encrypted;
            }

            await EnsureFolderAsync(cancellationToken);
            DropboxMetadata? uploaded;
            try
            {
                uploaded = await UploadRemoteAsync(uploadPath, expectedRemote?.Rev, cancellationToken);
            }
            catch (DropboxSyncException exception) when (exception.IsConflict)
            {
                return new DropboxSyncResult(
                    DropboxSyncStatus.Conflict,
                    LocalizationService.T("DropboxConflict"));
            }
            SetLastSync(uploaded?.ServerModified ?? DateTimeOffset.UtcNow);
            return new DropboxSyncResult(
                DropboxSyncStatus.Uploaded,
                LocalizationService.T("DropboxUploaded"));
        }
        finally
        {
            TryDelete(temporary);
            TryDelete(encrypted);
        }
    }

    private async Task<DropboxSyncResult> DownloadAsync(
        DropboxMetadata metadata,
        CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(
            _session.DataDirectory,
            $".dropbox-download-{Guid.NewGuid():N}.db");
        var plaintext = temporary + ".plain.db";
        try
        {
            await DownloadRemoteAsync(temporary, cancellationToken);
            var source = temporary;
            if (SyncEncryptionService.IsEncryptedFile(temporary))
            {
                var key = _session.Settings.DropboxSyncKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new DropboxSyncException(
                        LocalizationService.T("DropboxSyncKeyMissing"));
                }
                SyncEncryptionService.DecryptFile(temporary, plaintext, key);
                source = plaintext;
            }

            try
            {
                Database.ValidateFile(source);
            }
            catch (RestoreException)
            {
                // Backward compatibility with an old remote SQLCipher database.
                Database.ValidateFile(source, _session.Database.EncryptionKey);
            }

            var autoBackup = _session.Database.RestoreFrom(source);
            SetLastSync(metadata.ServerModified);
            return new DropboxSyncResult(
                DropboxSyncStatus.Downloaded,
                LocalizationService.T("DropboxDownloaded", autoBackup),
                autoBackup);
        }
        finally
        {
            TryDelete(temporary);
            TryDelete(plaintext);
        }
    }

    private Task<DropboxMetadata?> GetMetadataAsync(CancellationToken cancellationToken) =>
        WithRetryAsync(
            () => WithTokenAsync(
                token => _dropbox.GetMetadataAsync(token, RemotePath, cancellationToken),
                cancellationToken),
            cancellationToken);

    private Task<bool> EnsureFolderAsync(CancellationToken cancellationToken) =>
        WithRetryAsync(
            async () =>
            {
                await WithTokenAsync(
                    token => _dropbox.EnsureFolderAsync(token, GetRemoteFolder(), cancellationToken),
                    cancellationToken);
                return true;
            },
            cancellationToken);

    private Task<DropboxMetadata?> UploadRemoteAsync(
        string localPath,
        string? expectedRev,
        CancellationToken cancellationToken) =>
        WithRetryAsync(
            () => WithTokenAsync(
                token => _dropbox.UploadAsync(
                    token,
                    RemotePath,
                    localPath,
                    expectedRev,
                    cancellationToken),
                cancellationToken),
            cancellationToken);

    private Task<bool> DownloadRemoteAsync(string destinationPath, CancellationToken cancellationToken) =>
        WithRetryAsync(
            async () =>
            {
                await WithTokenAsync(
                    token => _dropbox.DownloadAsync(
                        token,
                        RemotePath,
                        destinationPath,
                        cancellationToken),
                    cancellationToken);
                return true;
            },
            cancellationToken);

    private static async Task<T> WithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (DropboxSyncException exception) when (exception.IsTransient && attempt < 2)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    cancellationToken);
            }
        }
    }

    private async Task<T> WithTokenAsync<T>(
        Func<string, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        try
        {
            return await action(token);
        }
        catch (DropboxSyncException exception) when (exception.IsUnauthorized)
        {
            token = await _tokenProvider.GetAccessTokenAsync(
                forceRefresh: true,
                cancellationToken: cancellationToken);
            return await action(token);
        }
    }

    private async Task WithTokenAsync(
        Func<string, Task> action,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        try
        {
            await action(token);
        }
        catch (DropboxSyncException exception) when (exception.IsUnauthorized)
        {
            token = await _tokenProvider.GetAccessTokenAsync(
                forceRefresh: true,
                cancellationToken: cancellationToken);
            await action(token);
        }
    }

    private string GetRemoteFolder()
    {
        var path = RemotePath;
        var index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path[..index];
    }

    private bool HasLocalData()
    {
        if (_session.Service.ListProjects(includeArchived: true).Count > 0 ||
            _session.Service.AllTimeBounds() is not null)
        {
            return true;
        }

        var defaultTypeNames = new HashSet<string>(
            ["Work", "Lunch", "Away", "X-Pause"],
            StringComparer.OrdinalIgnoreCase);
        return _session.Service.ListEntryTypes(includeArchived: true)
            .Any(type => !defaultTypeNames.Contains(type.Name));
    }

    private void SetLastSync(DateTimeOffset value)
    {
        _session.Settings.DropboxLastSyncUtc = value;
        _session.Settings.Save(_session.SettingsPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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
