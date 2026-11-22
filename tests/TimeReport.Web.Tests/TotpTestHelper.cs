using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace TimeReport.Web.Tests;

public static class TotpTestHelper
{
    [SuppressMessage(
        "Security",
        "CA5350",
        Justification = "RFC 6238 test vectors use HMAC-SHA1.")]
    public static string Code(string secret, DateTimeOffset? at = null)
    {
        var normalized = secret
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .TrimEnd('=')
            .ToUpperInvariant();
        var key = Base32Decode(normalized);
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        Span<byte> payload = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(payload, counter);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, payload, hash);
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) |
                    (hash[offset + 1] << 16) |
                    (hash[offset + 2] << 8) |
                    hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var character in value)
        {
            var index = alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException("Invalid base32 secret.");
            }
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xff));
            }
        }
        return [.. output];
    }
}
