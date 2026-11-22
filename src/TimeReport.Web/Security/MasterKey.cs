using System.Security.Cryptography;
using System.Text;
using TimeReport.Services;
using TimeReport.Web.Configuration;

namespace TimeReport.Web.Security;

/// <summary>
/// Root secret for the web deployment. It is never stored by the application.
/// The authentication database, every user database and the TOTP secret
/// protection key are derived from it with HKDF-SHA256.
/// </summary>
public sealed class MasterKey
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    private MasterKey(byte[] key)
    {
        _key = key;
    }

    public static MasterKey Load(WebOptions options)
    {
        string encoded;
        if (!string.IsNullOrWhiteSpace(options.MasterKeyFile))
        {
            var path = Path.GetFullPath(AppPaths.ExpandHome(options.MasterKeyFile));
            try
            {
                FileSafety.EnsureRegularFile(path, 64 * 1024, "master key file");
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    $"The master key file '{path}' is not a readable regular file.",
                    exception);
            }
            encoded = File.ReadAllText(path).Trim();
        }
        else
        {
            encoded = options.MasterKey?.Trim()
                ?? throw new InvalidOperationException("No master key is configured.");
        }

        var key = DecodeKey(encoded);
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                "The master key must be exactly 32 bytes, encoded as base64.");
        }

        return new MasterKey(key);
    }

    public byte[] Derive(string purpose, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("A derivation purpose is required.", nameof(purpose));
        }

        var salt = context is null
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(context);
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _key,
            KeySize,
            salt,
            Encoding.UTF8.GetBytes(purpose));
    }

    public string DeriveBase64(string purpose, string? context = null) =>
        Convert.ToBase64String(Derive(purpose, context));

    public string Protect(string plaintext, string purpose)
    {
        var key = Derive(purpose);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);
        return "v1:" + Convert.ToBase64String(payload);
    }

    public string Unprotect(string stored, string purpose)
    {
        if (!stored.StartsWith("v1:", StringComparison.Ordinal))
        {
            throw new CryptographicException("The protected value has an unsupported format.");
        }

        var payload = Convert.FromBase64String(stored[3..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The protected value is truncated.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(Derive(purpose), TagSize))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        return Encoding.UTF8.GetString(plain);
    }

    public string HashToken(string token, string purpose) =>
        Convert.ToBase64String(
            HMACSHA256.HashData(Derive(purpose), Encoding.UTF8.GetBytes(token)));

    public bool VerifyToken(string token, string expectedHash, string purpose)
    {
        var actual = HashToken(token, purpose);
        return FixedTimeEquals(actual, expectedHash);
    }

    public static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static byte[] DecodeKey(string encoded)
    {
        try
        {
            var decoded = Convert.FromBase64String(encoded);
            if (decoded.Length == KeySize)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        if (encoded.Length == 64)
        {
            try
            {
                var decoded = Convert.FromHexString(encoded);
                if (decoded.Length == KeySize)
                {
                    return decoded;
                }
            }
            catch (FormatException)
            {
            }
        }

        throw new InvalidOperationException(
            "The master key must be 32 bytes encoded as base64 (or 64 hexadecimal characters).");
    }
}
