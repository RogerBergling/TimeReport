using System.Net.Http.Headers;
using System.Text.Json;

namespace TimeReport.Services;

public sealed record UpdateInfo(
    string Tag,
    Version Version,
    string ReleaseUrl,
    string? Notes);

public static class UpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/RogerBergling/TimeReport/releases/latest";

    public static async Task<UpdateInfo?> CheckAsync(
        HttpClient httpClient,
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TimeReport", currentVersion));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new UpdateException(
                $"The update service returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var releaseUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
        var versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var version))
        {
            throw new UpdateException($"The update service returned an invalid version: {tag}");
        }

        if (Version.TryParse(currentVersion, out var current) && version <= current)
        {
            return null;
        }

        return new UpdateInfo(tag, version, releaseUrl, notes);
    }
}

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message)
    {
    }

    public UpdateException(string message, Exception inner) : base(message, inner)
    {
    }
}
