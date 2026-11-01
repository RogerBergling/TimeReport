using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task NewerReleaseIsReturned()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"tag_name":"v2.6.0","html_url":"https://example.test/v2.6.0","body":"Notes"}"""));
        using var client = new HttpClient(handler);

        var update = await UpdateService.CheckAsync(client, "2.5.0");

        Assert.NotNull(update);
        Assert.Equal("v2.6.0", update.Tag);
        Assert.Equal(new Version(2, 6, 0), update.Version);
        Assert.Equal("Notes", update.Notes);
    }

    [Fact]
    public async Task SameOrOlderReleaseIsNotAnUpdate()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"tag_name":"v2.5.0","html_url":"https://example.test/v2.5.0"}"""));
        using var client = new HttpClient(handler);

        Assert.Null(await UpdateService.CheckAsync(client, "2.5.0"));
    }

    [Fact]
    public async Task InvalidVersionIsRejected()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"tag_name":"not-a-version","html_url":"https://example.test"}"""));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<UpdateException>(() =>
            UpdateService.CheckAsync(client, "2.5.0"));
    }
}
