using System.Text.Json;

namespace TimeReport.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string? DatabasePath { get; set; }

    public string? DatabaseEncryptionKey { get; set; }

    public string? Language { get; set; }

    public string? Theme { get; set; }

    public string? TimeZoneId { get; set; }

    public int OvertimeDailyMinutes { get; set; }

    public int OvertimeWeeklyMinutes { get; set; }

    public int BackupRetentionCount { get; set; } = 20;

    public bool CheckForUpdates { get; set; } = true;

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public string? DropboxAppKey { get; set; }

    public string? DropboxAppSecret { get; set; }

    public string? DropboxRefreshToken { get; set; }

    public string? DropboxAccessToken { get; set; }

    public DateTimeOffset? DropboxAccessTokenExpiresUtc { get; set; }

    public string? DropboxRemotePath { get; set; }

    public string? DropboxSyncKey { get; set; }

    public bool DropboxSyncEncryptionEnabled { get; set; } = true;

    public string? DropboxProxyUrl { get; set; }

    public string? DropboxCaCertificatePath { get; set; }

    public bool DropboxAutoSync { get; set; }

    public DateTimeOffset? DropboxLastSyncUtc { get; set; }

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            TokenProtection.Initialize(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                ?? new AppSettings();
            var migrated = false;
            settings.DatabaseEncryptionKey = LoadSecret(settings.DatabaseEncryptionKey, ref migrated);
            settings.DropboxSyncKey = LoadSecret(settings.DropboxSyncKey, ref migrated);
            settings.DropboxAppSecret = LoadSecret(settings.DropboxAppSecret, ref migrated);
            settings.DropboxRefreshToken = LoadSecret(settings.DropboxRefreshToken, ref migrated);
            settings.DropboxAccessToken = LoadSecret(settings.DropboxAccessToken, ref migrated);
            if (migrated)
            {
                try
                {
                    settings.Save(path);
                }
                catch (Exception exception)
                {
                    AppLog.Warn("Could not re-encrypt the settings file.", exception);
                }
            }
            return settings;
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    private static string? LoadSecret(string? value, ref bool migrated)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        if (!TokenProtection.IsProtected(value))
        {
            migrated = true;
        }
        return TokenProtection.Unprotect(value);
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            AppPaths.EnsureDataDir(directory);
        }

        TokenProtection.Initialize(path);
        var stored = (AppSettings)MemberwiseClone();
        stored.DatabaseEncryptionKey = TokenProtection.Protect(DatabaseEncryptionKey);
        stored.DropboxSyncKey = TokenProtection.Protect(DropboxSyncKey);
        stored.DropboxAppSecret = TokenProtection.Protect(DropboxAppSecret);
        stored.DropboxRefreshToken = TokenProtection.Protect(DropboxRefreshToken);
        stored.DropboxAccessToken = TokenProtection.Protect(DropboxAccessToken);

        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(JsonSerializer.Serialize(stored, JsonOptions));
            }
            AppPaths.SecureFile(temporary);
            File.Move(temporary, path, true);
            AppPaths.SecureFile(path);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
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
