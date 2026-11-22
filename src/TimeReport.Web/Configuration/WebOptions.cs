using System.Globalization;
using System.Security.Cryptography;
using TimeReport.Services;

namespace TimeReport.Web.Configuration;

public sealed class WebOptions
{
    public string DataDir { get; init; } = DefaultDataDir();

    public string? MasterKey { get; set; }

    public string? MasterKeyFile { get; init; }

    public bool DemoMode { get; init; }

    public int DemoResetHours { get; init; } = 2;

    public string? SetupToken { get; init; }

    public string? SetupTokenFile { get; init; }

    public bool CookieSecure { get; init; } = true;

    public string TrustedProxies { get; init; } = "127.0.0.1/32,::1/128";

    public string AllowedHosts { get; init; } = "*";

    public int SessionMinutes { get; init; } = 30;

    public int SessionAbsoluteHours { get; init; } = 12;

    public int MfaMinutes { get; init; } = 5;

    public int LoginWindowMinutes { get; init; } = 15;

    public int MaxLoginAttemptsPerIp { get; init; } = 20;

    public int MaxLoginAttemptsPerUser { get; init; } = 8;

    public int MaxMfaAttempts { get; init; } = 5;

    public int MaxSetupAttempts { get; init; } = 10;

    public string Issuer { get; init; } = "TimeReport";

    public static WebOptions Load(IConfiguration configuration)
    {
        var options = new WebOptions
        {
            DataDir = FullPath(
                Get(configuration, "TIMEREPORT_DATA_DIR")
                ?? DefaultDataDir()),
            MasterKey = Get(configuration, "TIMEREPORT_MASTER_KEY"),
            MasterKeyFile = Get(configuration, "TIMEREPORT_MASTER_KEY_FILE"),
            DemoMode = Bool(configuration, "TIMEREPORT_DEMO_MODE", false),
            DemoResetHours = Int(configuration, "TIMEREPORT_DEMO_RESET_HOURS", 2, 1, 24),
            SetupToken = Get(configuration, "TIMEREPORT_SETUP_TOKEN"),
            SetupTokenFile = Get(configuration, "TIMEREPORT_SETUP_TOKEN_FILE"),
            CookieSecure = Bool(configuration, "TIMEREPORT_COOKIE_SECURE", true),
            TrustedProxies = Get(configuration, "TIMEREPORT_TRUSTED_PROXIES")
                ?? "127.0.0.1/32,::1/128",
            AllowedHosts = Get(configuration, "TIMEREPORT_ALLOWED_HOSTS") ?? "*",
            SessionMinutes = Int(configuration, "TIMEREPORT_SESSION_MINUTES", 30, 5, 1440),
            SessionAbsoluteHours = Int(configuration, "TIMEREPORT_SESSION_ABSOLUTE_HOURS", 12, 1, 168),
            MfaMinutes = Int(configuration, "TIMEREPORT_MFA_MINUTES", 5, 1, 15),
            LoginWindowMinutes = Int(configuration, "TIMEREPORT_LOGIN_WINDOW_MINUTES", 15, 1, 1440),
            MaxLoginAttemptsPerIp = Int(configuration, "TIMEREPORT_MAX_LOGIN_ATTEMPTS_PER_IP", 20, 3, 1000),
            MaxLoginAttemptsPerUser = Int(configuration, "TIMEREPORT_MAX_LOGIN_ATTEMPTS_PER_USER", 8, 3, 1000),
            MaxMfaAttempts = Int(configuration, "TIMEREPORT_MAX_MFA_ATTEMPTS", 5, 3, 20),
            MaxSetupAttempts = Int(configuration, "TIMEREPORT_MAX_SETUP_ATTEMPTS", 10, 3, 100),
            Issuer = Get(configuration, "TIMEREPORT_ISSUER") ?? "TimeReport",
        };

        if (string.IsNullOrWhiteSpace(options.MasterKey) &&
            string.IsNullOrWhiteSpace(options.MasterKeyFile))
        {
            if (options.DemoMode)
            {
                // The demo image is disposable. A fresh random key on every
                // start guarantees that old demo databases cannot be reused.
                options.MasterKey = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32));
            }
            else
            {
                throw new InvalidOperationException(
                    "No master key is configured. Set TIMEREPORT_MASTER_KEY_FILE to a " +
                    "file containing a base64-encoded 32-byte key, or set " +
                    "TIMEREPORT_MASTER_KEY. Generate one with " +
                    "scripts/generate-master-key.sh.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.MasterKey) &&
            !string.IsNullOrWhiteSpace(options.MasterKeyFile))
        {
            throw new InvalidOperationException(
                "Set either TIMEREPORT_MASTER_KEY or TIMEREPORT_MASTER_KEY_FILE, not both.");
        }

        if (!string.IsNullOrWhiteSpace(options.SetupToken) &&
            !string.IsNullOrWhiteSpace(options.SetupTokenFile))
        {
            throw new InvalidOperationException(
                "Set either TIMEREPORT_SETUP_TOKEN or TIMEREPORT_SETUP_TOKEN_FILE, not both.");
        }

        return options;
    }

    public string ReadSetupToken()
    {
        if (!string.IsNullOrWhiteSpace(SetupToken))
        {
            return SetupToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(SetupTokenFile))
        {
            return ReadRegularTextFile(SetupTokenFile, "setup token file");
        }

        return string.Empty;
    }

    private static string ReadRegularTextFile(string path, string description)
    {
        var fullPath = Path.GetFullPath(AppPaths.ExpandHome(path));
        try
        {
            FileSafety.EnsureRegularFile(fullPath, 64 * 1024, description);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"The {description} '{fullPath}' is not a readable regular file.",
                exception);
        }

        var value = File.ReadAllText(fullPath).Trim();
        if (value.Length == 0)
        {
            throw new InvalidOperationException($"The {description} '{fullPath}' is empty.");
        }

        return value;
    }

    private static string? Get(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(key);
        }
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Bool(IConfiguration configuration, string key, bool fallback) =>
        bool.TryParse(Get(configuration, key), out var parsed) ? parsed : fallback;

    private static int Int(
        IConfiguration configuration,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        var value = Get(configuration, key);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private static string DefaultDataDir()
    {
        var container = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        return container
            ? "/data"
            : Path.Combine(Directory.GetCurrentDirectory(), "data");
    }

    private static string FullPath(string path) =>
        Path.GetFullPath(AppPaths.ExpandHome(path));
}
