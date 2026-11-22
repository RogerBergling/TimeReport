using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TimeReport.Web.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    public TestWebApplicationFactory(bool demoMode = false)
    {
        DemoMode = demoMode;
        DataDirectory = Path.Combine(
            Path.GetTempPath(),
            "timereport-web-tests",
            Guid.NewGuid().ToString("N"));
        MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Program reads configuration before WebApplicationFactory's host
        // configuration callbacks run. Use the exact environment variables the
        // production container also uses.
        SetEnvironment("TIMEREPORT_DATA_DIR", DataDirectory);
        SetEnvironment("TIMEREPORT_MASTER_KEY", MasterKey);
        SetEnvironment("TIMEREPORT_COOKIE_SECURE", "false");
        SetEnvironment("TIMEREPORT_SETUP_TOKEN", SetupToken);
        SetEnvironment("TIMEREPORT_TRUSTED_PROXIES", "127.0.0.1/32");
        SetEnvironment("TIMEREPORT_DEMO_MODE", demoMode ? "true" : "false");
        SetEnvironment("TIMEREPORT_DEMO_RESET_HOURS", "2");
    }

    public bool DemoMode { get; }

    public string DataDirectory { get; }

    public string MasterKey { get; }

    public const string SetupToken = "test-setup-token-1234567890";

    public HttpClient CreateClientWithoutRedirects() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private void SetEnvironment(string key, string value)
    {
        _previousEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TIMEREPORT_DATA_DIR"] = DataDirectory,
                ["TIMEREPORT_MASTER_KEY"] = MasterKey,
                ["TIMEREPORT_COOKIE_SECURE"] = "false",
                ["TIMEREPORT_SETUP_TOKEN"] = SetupToken,
                ["TIMEREPORT_TRUSTED_PROXIES"] = "127.0.0.1/32",
                ["TIMEREPORT_DEMO_MODE"] = DemoMode ? "true" : "false",
                ["TIMEREPORT_DEMO_RESET_HOURS"] = "2",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        foreach (var (key, value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public static partial class TestHtml
{
    public static string AntiforgeryToken(string html)
    {
        var match = AntiforgeryPattern().Match(html);
        return match.Success
            ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException("No antiforgery token was found.");
    }

    public static string TotpSecret(string html)
    {
        var match = SecretPattern().Match(html);
        return match.Success
            ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Replace(" ", string.Empty, StringComparison.Ordinal)
            : throw new InvalidOperationException("No TOTP secret was found.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();

    [GeneratedRegex("id=\"totp-secret\">([^<]+)</span>")]
    private static partial Regex SecretPattern();
}
