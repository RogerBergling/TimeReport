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
    public const string CallbackPath = "/dropbox-callback";
    private const int CallbackPort = 53682;
    private const int MaxRequestBytes = 16 * 1024;
    private static readonly TimeSpan CallbackRequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<DropboxTokenResult> AuthorizeAsync(
        string appKey,
        string? appSecret,
        HttpClient? httpClient = null,
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
            Dictionary<string, string> query;
            try
            {
                query = await WaitForCallbackAsync(listener, state, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DropboxSyncException(
                    "Timed out waiting for Dropbox authorization.");
            }

            if (query.TryGetValue("error", out var error))
            {
                throw new DropboxSyncException(
                    query.TryGetValue("error_description", out var description)
                        ? description
                        : error);
            }
            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                throw new DropboxSyncException("Dropbox did not return an authorization code.");
            }

            return await ExchangeCodeAsync(appKey, appSecret, code, verifier, httpClient ?? SharedHttpClient, cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Accepts loopback connections until a valid GET /dropbox-callback request
    /// with the expected state arrives. Requests for other paths, malformed
    /// requests and mismatched states are answered and ignored so a stray browser
    /// request cannot terminate the authorization flow.
    /// </summary>
    internal static async Task<Dictionary<string, string>> WaitForCallbackAsync(
        TcpListener listener,
        string expectedState,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            try
            {
                var stream = client.GetStream();
                using var requestTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(CallbackRequestTimeout);
                var request = await ReadRequestAsync(stream, requestTimeout.Token);
                if (request is null)
                {
                    await TryWriteResponseAsync(
                        stream,
                        "400 Bad Request",
                        "The callback request was invalid.",
                        cancellationToken);
                    continue;
                }

                var (method, target) = request.Value;
                if (!string.Equals(method, "GET", StringComparison.Ordinal) ||
                    !TryGetCallbackQuery(target, out var query))
                {
                    await TryWriteResponseAsync(
                        stream,
                        "404 Not Found",
                        "This is not the Dropbox callback endpoint.",
                        cancellationToken);
                    continue;
                }

                if (!query.TryGetValue("state", out var returnedState) ||
                    !FixedTimeEquals(returnedState, expectedState))
                {
                    await TryWriteResponseAsync(
                        stream,
                        "400 Bad Request",
                        "The OAuth state did not match. Return to TimeReport and try again.",
                        cancellationToken);
                    continue;
                }

                await TryWriteResponseAsync(
                    stream,
                    "200 OK",
                    "Dropbox authorization complete. You can close this window.",
                    cancellationToken);
                return query;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Ignore slow or speculative connections and keep waiting.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                // The browser may open speculative connections or disconnect early.
            }
            catch (SocketException)
            {
            }
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
        HttpClient httpClient,
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
        using var response = await httpClient.PostAsync(
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

    private static async Task<(string Method, string Target)?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxRequestBytes];
        var count = 0;
        var headerEnd = -1;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken);
            if (read == 0)
            {
                break;
            }
            count += read;
            headerEnd = FindHeaderEnd(buffer.AsSpan(0, count));
            if (headerEnd >= 0)
            {
                break;
            }
        }

        if (count == 0 || headerEnd < 0)
        {
            return null;
        }

        var text = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        var requestLine = lineEnd >= 0 ? text[..lineEnd] : text;
        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }
        return (parts[0], parts[1]);
    }

    private static int FindHeaderEnd(ReadOnlySpan<byte> buffer)
    {
        for (var index = 0; index <= buffer.Length - 4; index++)
        {
            if (buffer[index] == '\r' &&
                buffer[index + 1] == '\n' &&
                buffer[index + 2] == '\r' &&
                buffer[index + 3] == '\n')
            {
                return index;
            }
        }
        return -1;
    }

    private static bool TryGetCallbackQuery(
        string target,
        out Dictionary<string, string> query)
    {
        query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!target.StartsWith('/') ||
            !Uri.TryCreate("http://127.0.0.1" + target, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.AbsolutePath, CallbackPath, StringComparison.Ordinal))
        {
            return false;
        }

        query = ParseQuery(uri.Query);
        return true;
    }

    private static bool FixedTimeEquals(string first, string second)
    {
        var firstHash = SHA256.HashData(Encoding.UTF8.GetBytes(first));
        var secondHash = SHA256.HashData(Encoding.UTF8.GetBytes(second));
        return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
    }

    private static async Task TryWriteResponseAsync(
        NetworkStream stream,
        string status,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var html =
                "<!doctype html><html><head><title>TimeReport</title></head>" +
                "<body style=\"font-family:sans-serif;padding:2rem\">" +
                $"<h2>TimeReport</h2><p>{WebUtility.HtmlEncode(message)}</p>" +
                "</body></html>";
            var body = Encoding.UTF8.GetBytes(html);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
        }
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
