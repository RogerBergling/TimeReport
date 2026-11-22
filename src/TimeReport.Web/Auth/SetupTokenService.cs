using System.Security.Cryptography;
using TimeReport.Web.Configuration;

namespace TimeReport.Web.Auth;

/// <summary>
/// First-run bootstrap token. The plaintext token is never stored: only its
/// SHA-256 hash stays in memory. A generated token is logged once at startup
/// and expires after 24 hours; a token supplied through the environment is
/// controlled by the operator.
/// </summary>
public sealed partial class SetupTokenService
{
    private readonly WebOptions _options;
    private readonly AuthDatabase _authDatabase;
    private readonly ILogger<SetupTokenService> _logger;
    private readonly object _sync = new();
    private string? _tokenHash;
    private DateTimeOffset? _expiresAtUtc;
    private bool _initialized;

    public SetupTokenService(
        WebOptions options,
        AuthDatabase authDatabase,
        ILogger<SetupTokenService> logger)
    {
        _options = options;
        _authDatabase = authDatabase;
        _logger = logger;
    }

    public void Initialize()
    {
        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            if (_authDatabase.CountUsers() > 0)
            {
                return;
            }

            var configured = _options.ReadSetupToken();
            string token;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (configured.Length < 16)
                {
                    throw new InvalidOperationException(
                        "TIMEREPORT_SETUP_TOKEN must be at least 16 characters.");
                }
                token = configured;
                _expiresAtUtc = null;
                LogConfiguredSetupToken(_logger);
            }
            else
            {
                token = GenerateToken();
                _expiresAtUtc = DateTimeOffset.UtcNow.AddHours(24);
                LogGeneratedSetupToken(_logger, token);
                LogSetupInstructions(_logger);
            }

            _tokenHash = Hash(token);
        }
    }

    public bool IsSetupRequired()
    {
        lock (_sync)
        {
            return _authDatabase.CountUsers() == 0;
        }
    }

    public bool Validate(string? token)
    {
        lock (_sync)
        {
            if (!IsSetupRequired() || _tokenHash is null)
            {
                return false;
            }

            if (_expiresAtUtc is { } expires && expires <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(Hash(token.Trim())),
                Convert.FromHexString(_tokenHash));
        }
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            _tokenHash = null;
            _expiresAtUtc = null;
            LogSetupCompleted(_logger);
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string GenerateToken()
    {
        var raw = Base32.Encode(RandomNumberGenerator.GetBytes(20));
        return string.Join(
            '-',
            raw[..8],
            raw[8..16],
            raw[16..24],
            raw[24..32]);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "First-run setup is available. Use the setup token supplied through the environment.")]
    private static partial void LogConfiguredSetupToken(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "First-run setup token (valid for 24 hours): {SetupToken}")]
    private static partial void LogGeneratedSetupToken(ILogger logger, string setupToken);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Open /setup and create the first administrator. The token is invalidated after setup.")]
    private static partial void LogSetupInstructions(ILogger logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "First-run setup completed; the setup token is now invalid.")]
    private static partial void LogSetupCompleted(ILogger logger);
}
