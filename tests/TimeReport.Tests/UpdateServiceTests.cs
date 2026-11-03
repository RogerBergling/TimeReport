using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class UpdateServiceTests
{
    private const string ValidReleaseUrl =
        "https://github.com/RogerBergling/TimeReport/releases/tag/v2.6.0";

    [Fact]
    public async Task NewerReleaseIsReturned()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            $$"""{"tag_name":"v2.6.0","html_url":"{{ValidReleaseUrl}}","body":"Notes"}"""));
        using var client = new HttpClient(handler);

        var update = await UpdateService.CheckAsync(client, "2.5.0");

        Assert.NotNull(update);
        Assert.Equal("v2.6.0", update.Tag);
        Assert.Equal(new Version(2, 6, 0), update.Version);
        Assert.Equal("Notes", update.Notes);
        Assert.Equal(ValidReleaseUrl, update.ReleaseUrl);
    }

    [Fact]
    public async Task SameOrOlderReleaseIsNotAnUpdate()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            $$"""{"tag_name":"v2.5.0","html_url":"{{ValidReleaseUrl}}"}"""));
        using var client = new HttpClient(handler);

        Assert.Null(await UpdateService.CheckAsync(client, "2.5.0"));
    }

    [Fact]
    public async Task InvalidVersionIsRejected()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            $$"""{"tag_name":"not-a-version","html_url":"{{ValidReleaseUrl}}"}"""));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<UpdateException>(() =>
            UpdateService.CheckAsync(client, "2.5.0"));
    }

    [Theory]
    [InlineData("file:///tmp/evil")]
    [InlineData("http://github.com/RogerBergling/TimeReport/releases/tag/v2.6.0")]
    [InlineData("https://example.test/RogerBergling/TimeReport/releases/tag/v2.6.0")]
    [InlineData("https://github.com/Other/Repository/releases/tag/v2.6.0")]
    public async Task UntrustedReleaseUrlIsRejected(string releaseUrl)
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            $$"""{"tag_name":"v2.6.0","html_url":"{{releaseUrl}}"}"""));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<UpdateException>(() =>
            UpdateService.CheckAsync(client, "2.5.0"));
    }

    [Theory]
    [InlineData("https://github.com/RogerBergling/TimeReport/releases/tag/v2.6.0", true)]
    [InlineData("https://github.com/RogerBergling/TimeReport/releases/latest", true)]
    [InlineData("file:///tmp/evil", false)]
    [InlineData("https://example.test/RogerBergling/TimeReport/releases/tag/v2.6.0", false)]
    [InlineData("https://github.com/Other/Repository/releases/tag/v2.6.0", false)]
    public void ReleaseUrlTrustCheck(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsTrustedReleaseUrl(url));
    }
}
