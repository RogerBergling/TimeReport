using System.Net;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class DropboxTokenProviderTests
{
    [Fact]
    public async Task ReturnsExistingAccessTokenWhenItIsValid()
    {
        using var context = new SessionContext();
        context.Session.Settings.DropboxAccessToken = "existing";
        context.Session.Settings.DropboxAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);
        var provider = new DropboxTokenProvider(context.Session);

        Assert.Equal("existing", await provider.GetAccessTokenAsync());
    }

    [Fact]
    public async Task RefreshesExpiredAccessTokenAndSavesResult()
    {
        using var context = new SessionContext();
        context.Session.Settings.DropboxAppKey = "app-key";
        context.Session.Settings.DropboxRefreshToken = "refresh-token";
        context.Session.Settings.DropboxAccessToken = "expired";
        context.Session.Settings.DropboxAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Session.Settings.Save(context.Session.SettingsPath);

        var handler = new FakeHttpMessageHandler();
        handler.Responses.Enqueue(_ => FakeHttpMessageHandler.Json(
            """{"access_token":"fresh","expires_in":14400,"token_type":"bearer"}"""));
        using var client = new HttpClient(handler);
        var provider = new DropboxTokenProvider(context.Session, client);

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("fresh", token);
        Assert.Equal("fresh", context.Session.Settings.DropboxAccessToken);
        Assert.NotNull(context.Session.Settings.DropboxAccessTokenExpiresUtc);
        Assert.True(context.Session.Settings.DropboxAccessTokenExpiresUtc > DateTimeOffset.UtcNow);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.dropboxapi.com/oauth2/token", request.Uri);
        var body = System.Text.Encoding.UTF8.GetString(request.Body);
        Assert.Contains("grant_type=refresh_token", body);
        Assert.Contains("refresh_token=refresh-token", body);
        Assert.Contains("client_id=app-key", body);
    }

    [Fact]
    public async Task ThrowsWhenNotConnected()
    {
        using var context = new SessionContext();
        var provider = new DropboxTokenProvider(context.Session);

        await Assert.ThrowsAsync<DropboxSyncException>(() => provider.GetAccessTokenAsync());
    }

    [Fact]
    public void DisconnectClearsTokens()
    {
        using var context = new SessionContext();
        context.Session.Settings.DropboxAccessToken = "token";
        context.Session.Settings.DropboxRefreshToken = "refresh";
        context.Session.Settings.DropboxAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);
        var provider = new DropboxTokenProvider(context.Session);

        provider.Disconnect();

        Assert.Null(context.Session.Settings.DropboxAccessToken);
        Assert.Null(context.Session.Settings.DropboxRefreshToken);
        Assert.Null(context.Session.Settings.DropboxAccessTokenExpiresUtc);
        Assert.False(provider.IsConnected);
    }

    private sealed class SessionContext : IDisposable
    {
        public SessionContext()
        {
            Directory = Path.Combine(
                Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Session = new AppSession(
                databaseOverride: Path.Combine(Directory, "timereport.db"),
                settingsPath: Path.Combine(Directory, "settings.json"));
        }

        public string Directory { get; }

        public AppSession Session { get; }

        public void Dispose()
        {
            Session.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, true);
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

public sealed class DropboxOAuthServiceTests
{
    [Fact]
    public void BuildAuthorizeUrlContainsRequiredParameters()
    {
        var url = DropboxOAuthService.BuildAuthorizeUrl("app-key", "challenge", "state");

        Assert.Contains("client_id=app-key", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge=challenge", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("state=state", url);
        Assert.Contains("token_access_type=offline", url);
        Assert.Contains(Uri.EscapeDataString(DropboxOAuthService.RedirectUri), url);
    }
}
