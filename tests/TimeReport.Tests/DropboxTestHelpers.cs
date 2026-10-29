using System.Net;
using System.Text;
using TimeReport.Services;

namespace TimeReport.Tests;

internal sealed record CapturedRequest(
    string Method,
    string Uri,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body);

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    public Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? []
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(
            header => header.Key,
            header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);
        Requests.Add(new CapturedRequest(
            request.Method.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            headers,
            body));

        if (Responses.Count == 0)
        {
            throw new InvalidOperationException("No fake HTTP response was queued.");
        }
        return Responses.Dequeue()(request);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Bytes(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
}

internal sealed class FakeDropboxSyncService : IDropboxSyncService
{
    public DropboxMetadata? RemoteMetadata { get; set; }

    public byte[]? RemoteContent { get; set; }

    public int UploadCount { get; private set; }

    public int DownloadCount { get; private set; }

    public Task TestConnectionAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<DropboxMetadata?> GetMetadataAsync(
        string accessToken,
        string remotePath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoteMetadata);

    public Task<DropboxMetadata?> UploadAsync(
        string accessToken,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        UploadCount++;
        RemoteContent = File.ReadAllBytes(localPath);
        RemoteMetadata = new DropboxMetadata(DateTimeOffset.UtcNow, "rev", null);
        return Task.FromResult<DropboxMetadata?>(RemoteMetadata);
    }

    public Task DownloadAsync(
        string accessToken,
        string remotePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        DownloadCount++;
        File.WriteAllBytes(destinationPath, RemoteContent ?? []);
        return Task.CompletedTask;
    }

    public Task EnsureFolderAsync(
        string accessToken,
        string remoteFolderPath,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
