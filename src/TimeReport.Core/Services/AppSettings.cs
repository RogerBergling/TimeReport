using System.Security.Cryptography;
using System.Text.Json;

namespace TimeReport.Services;

public sealed class SettingsException : Exception
{
    public SettingsException(string message) : base(message)
    {
    }

    public SettingsException(string message, Exception inner) : base(message, inner)
    {
    }
}

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

    /// <summary>
    /// Protected copy of the passphrase used to derive version 3 sync container
    /// keys. Legacy derived keys remain in <see cref="DropboxSyncKey"/>.
    /// </summary>
    public string? DropboxSyncPassphrase { get; set; }

    public bool DropboxSyncEncryptionEnabled { get; set; } = true;

    public string? DropboxProxyUrl { get; set; }

    public string? DropboxCaCertificatePath { get; set; }

    public bool DropboxAutoSync { get; set; }

    public DateTimeOffset? DropboxLastSyncUtc { get; set; }

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            if (File.Exists(path + ".key"))
            {
                throw new SettingsException(
                    "settings.json is missing but settings.json.key still exists. " +
                    "Restore settings.json from backup before starting TimeReport.");
            }
            return new AppSettings();
        }

        TokenProtection.Initialize(path);
        string json;
        try
        {
            FileSafety.EnsureRegularFile(path, FileSafety.MaxSettingsBytes, "settings file");
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsException(
                $"Could not read the settings file '{path}': {exception.Message}. " +
                "Restore it from backup before starting TimeReport.",
                exception);
        }

        AppSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? throw new SettingsException(
                    $"The settings file '{path}' is empty or invalid. Restore it from backup.");
        }
        catch (JsonException exception)
        {
            throw new SettingsException(
                $"The settings file '{path}' is not valid JSON. Restore it from backup.",
                exception);
        }

        var migrated = false;
        settings.DatabaseEncryptionKey = LoadSecret(
            settings.DatabaseEncryptionKey,
            "database encryption key",
            required: true,
            ref migrated);
        settings.DropboxSyncKey = LoadSecret(
            settings.DropboxSyncKey,
            "Dropbox sync key",
            required: true,
            ref migrated);
        settings.DropboxSyncPassphrase = LoadSecret(
            settings.DropboxSyncPassphrase,
            "Dropbox sync passphrase",
            required: false,
            ref migrated);
        settings.DropboxAppSecret = LoadSecret(
            settings.DropboxAppSecret,
            "Dropbox app secret",
            required: false,
            ref migrated);
        settings.DropboxRefreshToken = LoadSecret(
            settings.DropboxRefreshToken,
            "Dropbox refresh token",
            required: false,
            ref migrated);
        settings.DropboxAccessToken = LoadSecret(
            settings.DropboxAccessToken,
            "Dropbox access token",
            required: false,
            ref migrated);
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

    private static string? LoadSecret(
        string? value,
        string name,
        bool required,
        ref bool migrated)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        if (!TokenProtection.IsProtected(value))
        {
            migrated = true;
            return value;
        }

        try
        {
            return TokenProtection.Unprotect(value);
        }
        catch (CryptographicException exception)
        {
            if (required)
            {
                throw new SettingsException(
                    $"Could not decrypt the {name}. Restore settings.json and settings.json.key from backup.",
                    exception);
            }

            AppLog.Warn($"Could not decrypt the {name}.", exception);
            return null;
        }
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
        stored.DropboxSyncPassphrase = TokenProtection.Protect(DropboxSyncPassphrase);
        stored.DropboxAppSecret = TokenProtection.Protect(DropboxAppSecret);
        stored.DropboxRefreshToken = TokenProtection.Protect(DropboxRefreshToken);
        stored.DropboxAccessToken = TokenProtection.Protect(DropboxAccessToken);

        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = AppPaths.CreatePrivateFileStream(
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
