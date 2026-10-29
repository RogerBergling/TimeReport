using System.Net;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class DropboxSyncServiceTests
{
    private const string MetadataJson =
        """{"name":"timereport.db","path_display":"/TimeReport/timereport.db","rev":"abc123","server_modified":"2026-10-29T12:34:56Z","content_hash":"hash"}""";

    [Fact]
    public async Task GetMetadataParsesResponseAndSendsToken()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(MetadataJson));
        using var client = new HttpClient(handler);
        var service = new DropboxSyncService(client);

        var metadata = await service.GetMetadataAsync("token", "/TimeReport/timereport.db");

        Assert.NotNull(metadata);
        Assert.Equal("abc123", metadata.Rev);
        Assert.Equal("hash", metadata.ContentHash);
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T12:34:56Z"), metadata.ServerModified);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.dropboxapi.com/2/files/get_metadata", request.Uri);
        Assert.Equal("Bearer token", request.Headers["Authorization"]);
        Assert.Contains("/TimeReport/timereport.db", System.Text.Encoding.UTF8.GetString(request.Body));
    }

    [Fact]
    public async Task GetMetadataReturnsNullWhenRemoteFileDoesNotExist()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"error_summary":"path/not_found/..","error":{".tag":"path"}}""",
            HttpStatusCode.Conflict));
        using var client = new HttpClient(handler);
        var service = new DropboxSyncService(client);

        Assert.Null(await service.GetMetadataAsync("token", "/missing.db"));
    }

    [Fact]
    public async Task UploadSendsFileBytesAndDropboxApiArg()
    {
        var directory = NewTempDirectory();
        try
        {
            var localPath = Path.Combine(directory, "local.db");
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            await File.WriteAllBytesAsync(localPath, bytes);
            var handler = new FakeHttpMessageHandler();
            handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(MetadataJson));
            using var client = new HttpClient(handler);
            var service = new DropboxSyncService(client);

            var metadata = await service.UploadAsync(
                "token",
                "/TimeReport/timereport.db",
                localPath);

            Assert.NotNull(metadata);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("https://content.dropboxapi.com/2/files/upload", request.Uri);
            Assert.Equal("Bearer token", request.Headers["Authorization"]);
            Assert.Contains("/TimeReport/timereport.db", request.Headers["Dropbox-API-Arg"]);
            Assert.Equal(bytes, request.Body);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DownloadWritesResponseBytes()
    {
        var directory = NewTempDirectory();
        try
        {
            var destination = Path.Combine(directory, "downloaded.db");
            var bytes = new byte[] { 9, 8, 7 };
            var handler = new FakeHttpMessageHandler();
            handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Bytes(bytes));
            using var client = new HttpClient(handler);
            var service = new DropboxSyncService(client);

            await service.DownloadAsync("token", "/TimeReport/timereport.db", destination);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
            var request = Assert.Single(handler.Requests);
            Assert.Equal("https://content.dropboxapi.com/2/files/download", request.Uri);
            Assert.Contains("/TimeReport/timereport.db", request.Headers["Dropbox-API-Arg"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task EnsureFolderIgnoresExistingFolderConflict()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"error_summary":"path/conflict/folder/..","error":{".tag":"path"}}""",
            HttpStatusCode.Conflict));
        using var client = new HttpClient(handler);
        var service = new DropboxSyncService(client);

        await service.EnsureFolderAsync("token", "/TimeReport");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.dropboxapi.com/2/files/create_folder_v2", request.Uri);
        Assert.Contains("/TimeReport", System.Text.Encoding.UTF8.GetString(request.Body));
    }

    [Fact]
    public async Task TestConnectionUsesListFolder()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"entries":[],"cursor":"cursor","has_more":false}"""));
        using var client = new HttpClient(handler);
        var service = new DropboxSyncService(client);

        await service.TestConnectionAsync("token");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.dropboxapi.com/2/files/list_folder", request.Uri);
    }

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
