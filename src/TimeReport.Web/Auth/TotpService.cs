using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace TimeReport.Web.Auth;

public sealed record TotpValidationResult(bool Success, long Step);

public sealed class TotpService
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;
    private const int Window = 1;
    private const int SecretBytes = 20;

    public string GenerateSecret() =>
        Base32.Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    public string BuildUri(string issuer, string account, string secret)
    {
        var label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}";
        var query = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"secret={Uri.EscapeDataString(secret)}&issuer={Uri.EscapeDataString(issuer)}" +
            $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}");
        return $"otpauth://totp/{label}?{query}";
    }

    public string QrCodeDataUrl(string uri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        var png = code.GetGraphic(6);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    public string GenerateCode(string secret, DateTimeOffset at)
    {
        var key = Base32.Decode(secret);
        if (key.Length < 10)
        {
            throw new FormatException("The TOTP secret is too short.");
        }

        return Hotp(key, at.ToUnixTimeSeconds() / PeriodSeconds);
    }

    public TotpValidationResult Validate(
        string secret,
        string? code,
        long lastAcceptedStep,
        DateTimeOffset now)
    {
        var normalized = (code ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length != Digits || !normalized.All(char.IsAsciiDigit))
        {
            return new TotpValidationResult(false, 0);
        }

        byte[] key;
        try
        {
            key = Base32.Decode(secret);
        }
        catch (FormatException)
        {
            return new TotpValidationResult(false, 0);
        }

        if (key.Length < 10)
        {
            return new TotpValidationResult(false, 0);
        }

        var currentStep = now.ToUnixTimeSeconds() / PeriodSeconds;
        for (var offset = -Window; offset <= Window; offset++)
        {
            var step = currentStep + offset;
            if (step < 0 || step <= lastAcceptedStep)
            {
                continue;
            }

            var expected = Hotp(key, step);
            if (FixedTimeEquals(expected, normalized))
            {
                return new TotpValidationResult(true, step);
            }
        }

        return new TotpValidationResult(false, 0);
    }

    public static string FormatSecretForDisplay(string secret)
    {
        var normalized = Base32.Normalize(secret);
        var builder = new StringBuilder(normalized.Length + normalized.Length / 4);
        for (var index = 0; index < normalized.Length; index++)
        {
            if (index > 0 && index % 4 == 0)
            {
                builder.Append(' ');
            }
            builder.Append(normalized[index]);
        }
        return builder.ToString();
    }

    [SuppressMessage(
        "Security",
        "CA5350",
        Justification = "RFC 6238 TOTP uses HMAC-SHA1 for authenticator-app compatibility.")]
    private static string Hotp(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) |
                    (hash[offset + 1] << 16) |
                    (hash[offset + 2] << 8) |
                    hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
