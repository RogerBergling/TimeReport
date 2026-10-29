using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TimeReport.Services;

public sealed class DropboxSyncException : Exception
{
    public DropboxSyncException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? inner = null) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}

public sealed record DropboxMetadata(
    DateTimeOffset ServerModified,
    string Rev,
    string? ContentHash);

public interface IDropboxSyncService
{
    Task TestConnectionAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<DropboxMetadata?> GetMetadataAsync(
        string accessToken,
        string remotePath,
        CancellationToken cancellationToken = default);

    Task<DropboxMetadata?> UploadAsync(
        string accessToken,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string accessToken,
        string remotePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task EnsureFolderAsync(
        string accessToken,
        string remoteFolderPath,
        CancellationToken cancellationToken = default);
}

public sealed class DropboxSyncService : IDropboxSyncService
{
    private const string ApiBase = "https://api.dropboxapi.com/2/";
    private const string ContentBase = "https://content.dropboxapi.com/2/";
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly HttpClient _httpClient;

    public DropboxSyncService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task TestConnectionAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateApiRequest(
            accessToken,
            "files/list_folder",
            new { path = string.Empty, limit = 1, recursive = false });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<DropboxMetadata?> GetMetadataAsync(
        string accessToken,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateApiRequest(
            accessToken,
            "files/get_metadata",
            new
            {
                path = remotePath,
                include_media_info = false,
                include_deleted = false,
            });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsPathNotFound(body))
            {
                return null;
            }
            throw new DropboxSyncException(ExtractError(body), response.StatusCode);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMetadata(json);
    }

    public async Task<DropboxMetadata?> UploadAsync(
        string accessToken,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ContentBase}files/upload");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add(
            "Dropbox-API-Arg",
            JsonSerializer.Serialize(new
            {
                path = remotePath,
                mode = "overwrite",
                autorename = false,
                mute = false,
            }));
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMetadata(json);
    }

    public async Task DownloadAsync(
        string accessToken,
        string remotePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ContentBase}files/download");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add(
            "Dropbox-API-Arg",
            JsonSerializer.Serialize(new { path = remotePath }));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new DropboxSyncException(ExtractError(body), response.StatusCode);
        }

        await using var stream = File.Create(destinationPath);
        await response.Content.CopyToAsync(stream, cancellationToken);
    }

    public async Task EnsureFolderAsync(
        string accessToken,
        string remoteFolderPath,
        CancellationToken cancellationToken = default)
    {
        var folder = remoteFolderPath.Trim();
        if (string.IsNullOrWhiteSpace(folder) || folder == "/")
        {
            return;
        }

        var current = string.Empty;
        foreach (var segment in folder.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + segment;
            using var request = CreateApiRequest(
                accessToken,
                "files/create_folder_v2",
                new { path = current, autorename = false });
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (body.Contains("path/conflict/folder", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("folder", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                throw new DropboxSyncException(ExtractError(body), response.StatusCode);
            }

            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    private static HttpRequestMessage CreateApiRequest(
        string accessToken,
        string endpoint,
        object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new DropboxSyncException(ExtractError(body), response.StatusCode);
    }

    private static DropboxMetadata ParseMetadata(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var serverModifiedText = root.GetProperty("server_modified").GetString()
            ?? throw new DropboxSyncException("Dropbox did not return a modification time.");
        var serverModified = DateTimeOffset.Parse(
            serverModifiedText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var rev = root.TryGetProperty("rev", out var revElement)
            ? revElement.GetString() ?? string.Empty
            : string.Empty;
        var contentHash = root.TryGetProperty("content_hash", out var hashElement)
            ? hashElement.GetString()
            : null;
        return new DropboxMetadata(serverModified, rev, contentHash);
    }

    private static bool IsPathNotFound(string body) =>
        body.Contains("path/not_found", StringComparison.OrdinalIgnoreCase) ||
        (body.Contains("\"not_found\"", StringComparison.OrdinalIgnoreCase) &&
         body.Contains("\"path\"", StringComparison.OrdinalIgnoreCase));

    private static string ExtractError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error_summary", out var summary))
            {
                return summary.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body) ? "Dropbox returned an unknown error." : body;
    }
}
