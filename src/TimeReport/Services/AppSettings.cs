using System.Text.Json;

namespace TimeReport.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string? DatabasePath { get; set; }

    public string? Language { get; set; }

    public string? DropboxAppKey { get; set; }

    public string? DropboxAppSecret { get; set; }

    public string? DropboxRefreshToken { get; set; }

    public string? DropboxAccessToken { get; set; }

    public DateTimeOffset? DropboxAccessTokenExpiresUtc { get; set; }

    public string? DropboxRemotePath { get; set; }

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
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
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
