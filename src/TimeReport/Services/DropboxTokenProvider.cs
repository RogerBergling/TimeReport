using System.Net.Http.Headers;
using System.Text.Json;

namespace TimeReport.Services;

public sealed class DropboxTokenProvider
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly AppSession _session;
    private readonly HttpClient _httpClient;

    public DropboxTokenProvider(AppSession session, HttpClient? httpClient = null)
    {
        _session = session;
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public bool IsConnected =>
        !string.IsNullOrWhiteSpace(_session.Settings.DropboxRefreshToken) ||
        !string.IsNullOrWhiteSpace(_session.Settings.DropboxAccessToken);

    public async Task<string> GetAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var settings = _session.Settings;
        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(settings.DropboxAccessToken) &&
            (settings.DropboxAccessTokenExpiresUtc is null ||
             settings.DropboxAccessTokenExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(1)))
        {
            return settings.DropboxAccessToken;
        }

        if (!string.IsNullOrWhiteSpace(settings.DropboxRefreshToken) &&
            !string.IsNullOrWhiteSpace(settings.DropboxAppKey))
        {
            return await RefreshAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(settings.DropboxAccessToken))
        {
            return settings.DropboxAccessToken;
        }

        throw new DropboxSyncException(LocalizationService.T("DropboxNotConnected"));
    }

    public async Task ConnectAsync(
        string appKey,
        string? appSecret,
        CancellationToken cancellationToken = default)
    {
        var result = await DropboxOAuthService.AuthorizeAsync(appKey, appSecret, cancellationToken);
        var settings = _session.Settings;
        settings.DropboxAppKey = appKey;
        settings.DropboxAppSecret = string.IsNullOrWhiteSpace(appSecret) ? null : appSecret;
        settings.DropboxRefreshToken = result.RefreshToken;
        settings.DropboxAccessToken = result.AccessToken;
        settings.DropboxAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);
        settings.DropboxLastSyncUtc = null;
        settings.Save(_session.SettingsPath);
    }

    public void Disconnect()
    {
        var settings = _session.Settings;
        settings.DropboxAccessToken = null;
        settings.DropboxRefreshToken = null;
        settings.DropboxAccessTokenExpiresUtc = null;
        settings.DropboxLastSyncUtc = null;
        settings.Save(_session.SettingsPath);
    }

    private async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        var settings = _session.Settings;
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = settings.DropboxRefreshToken!,
            ["client_id"] = settings.DropboxAppKey!,
        };
        if (!string.IsNullOrWhiteSpace(settings.DropboxAppSecret))
        {
            fields["client_secret"] = settings.DropboxAppSecret;
        }

        using var content = new FormUrlEncodedContent(fields);
        using var response = await _httpClient.PostAsync(
            "https://api.dropboxapi.com/oauth2/token",
            content,
            cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DropboxSyncException(ExtractError(json), response.StatusCode);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new DropboxSyncException("Dropbox did not return an access token.");
        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 14_400;
        var refreshToken = root.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString()
            : settings.DropboxRefreshToken;

        settings.DropboxAccessToken = accessToken;
        settings.DropboxAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        settings.DropboxRefreshToken = refreshToken;
        settings.Save(_session.SettingsPath);
        return accessToken;
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error_description", out var description))
            {
                return description.GetString() ?? body;
            }
            if (root.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }
        return string.IsNullOrWhiteSpace(body) ? "Dropbox token refresh failed." : body;
    }
}
