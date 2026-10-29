using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TimeReport.Services;

public sealed record DropboxTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? Scope,
    string? AccountId);

public static class DropboxOAuthService
{
    public const string RedirectUri = "http://127.0.0.1:53682/dropbox-callback";
    private const int CallbackPort = 53682;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<DropboxTokenResult> AuthorizeAsync(
        string appKey,
        string? appSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            throw new DropboxSyncException("Enter the Dropbox app key.");
        }

        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = CreateState();
        var authorizeUrl = BuildAuthorizeUrl(appKey, challenge, state);

        var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            throw new DropboxSyncException(
                $"Could not start the local Dropbox callback listener on port {CallbackPort}: {exception.Message}",
                null,
                exception);
        }

        try
        {
            OpenBrowser(authorizeUrl);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var query = await ReadCallbackAsync(client, timeout.Token);

            if (query.TryGetValue("error", out var error))
            {
                throw new DropboxSyncException(
                    query.TryGetValue("error_description", out var description)
                        ? description
                        : error);
            }
            if (!query.TryGetValue("state", out var returnedState) ||
                !string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                throw new DropboxSyncException("Dropbox returned an invalid OAuth state.");
            }
            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                throw new DropboxSyncException("Dropbox did not return an authorization code.");
            }

            return await ExchangeCodeAsync(appKey, appSecret, code, verifier, cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    public static string BuildAuthorizeUrl(string appKey, string codeChallenge, string state) =>
        "https://www.dropbox.com/oauth2/authorize" +
        $"?client_id={Uri.EscapeDataString(appKey)}" +
        "&response_type=code" +
        $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
        $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
        "&code_challenge_method=S256" +
        $"&state={Uri.EscapeDataString(state)}" +
        "&token_access_type=offline";

    private static async Task<DropboxTokenResult> ExchangeCodeAsync(
        string appKey,
        string? appSecret,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = appKey,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier,
        };
        if (!string.IsNullOrWhiteSpace(appSecret))
        {
            fields["client_secret"] = appSecret;
        }

        using var content = new FormUrlEncodedContent(fields);
        using var response = await HttpClient.PostAsync(
            "https://api.dropboxapi.com/oauth2/token",
            content,
            cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DropboxSyncException(ExtractTokenError(json), response.StatusCode);
        }

        return ParseToken(json);
    }

    private static DropboxTokenResult ParseToken(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new DropboxSyncException("Dropbox did not return an access token.");
        var refreshToken = root.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString()
            : null;
        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 14_400;
        var scope = root.TryGetProperty("scope", out var scopeElement)
            ? scopeElement.GetString()
            : null;
        var accountId = root.TryGetProperty("account_id", out var account)
            ? account.GetString()
            : null;
        return new DropboxTokenResult(accessToken, refreshToken, expiresIn, scope, accountId);
    }

    private static async Task<Dictionary<string, string>> ReadCallbackAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
        string? headerLine;
        do
        {
            headerLine = await reader.ReadLineAsync(cancellationToken);
        } while (!string.IsNullOrEmpty(headerLine));

        var parts = requestLine.Split(' ');
        var target = parts.Length > 1 ? parts[1] : "/";
        var uri = new Uri("http://127.0.0.1" + target);
        var query = ParseQuery(uri.Query);

        const string html =
            "<!doctype html><html><head><title>TimeReport</title></head>" +
            "<body style=\"font-family:sans-serif;padding:2rem\">" +
            "<h2>TimeReport</h2><p>Dropbox authorization complete. You can close this window.</p>" +
            "</body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        return query;
    }

    private static Dictionary<string, string> ParseQuery(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = queryString.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            result[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
        }
        return result;
    }

    private static string CreateCodeVerifier() =>
        Base64Url(RandomNumberGenerator.GetBytes(64));

    private static string CreateCodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string CreateState() =>
        Base64Url(RandomNumberGenerator.GetBytes(24));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw new DropboxSyncException(
                $"Could not open the browser. Open this URL manually:\n{url}",
                null,
                exception);
        }
    }

    private static string ExtractTokenError(string body)
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
        return string.IsNullOrWhiteSpace(body) ? "Dropbox authorization failed." : body;
    }
}
