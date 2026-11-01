using System.Security.Cryptography;
using System.Text;

namespace TimeReport.Services;

/// <summary>
/// Encrypts Dropbox secrets before they are written to settings.json.
/// The AES key lives in a separate owner-only file. On Windows that key file
/// is additionally protected with the current user's DPAPI key.
/// </summary>
public static class TokenProtection
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private static readonly object Sync = new();
    private static string? _keyPath;
    private static byte[]? _key;

    public static void Initialize(string settingsPath)
    {
        var keyPath = settingsPath + ".key";
        lock (Sync)
        {
            if (!string.Equals(_keyPath, keyPath, StringComparison.Ordinal))
            {
                _keyPath = keyPath;
                _key = null;
            }
        }
    }

    public static bool IsProtected(string? value) =>
        value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public static string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || IsProtected(plaintext))
        {
            return plaintext;
        }

        var key = GetKey();
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
        return Prefix + Convert.ToBase64String(payload);
    }

    public static string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !IsProtected(stored))
        {
            return stored;
        }

        try
        {
            var payload = Convert.FromBase64String(stored[Prefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
            {
                throw new CryptographicException("The protected token is truncated.");
            }

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var cipher = payload.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(GetKey(), TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception exception)
        {
            AppLog.Warn("Could not decrypt a stored Dropbox token.", exception);
            return null;
        }
    }

    private static byte[] GetKey()
    {
        lock (Sync)
        {
            if (_key is not null)
            {
                return _key;
            }
            if (string.IsNullOrWhiteSpace(_keyPath))
            {
                throw new InvalidOperationException("Token protection has not been initialized.");
            }

            var directory = Path.GetDirectoryName(_keyPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                AppPaths.EnsureDataDir(directory);
            }

            if (File.Exists(_keyPath))
            {
                var stored = File.ReadAllBytes(_keyPath);
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        _key = DpapiUnprotect(stored);
                    }
                    catch (CryptographicException) when (stored.Length == KeySize)
                    {
                        // Migrate the legacy unprotected key file written by 2.5.x.
                        _key = stored;
                        File.WriteAllBytes(_keyPath, DpapiProtect(stored));
                        AppPaths.SecureFile(_keyPath);
                    }
                }
                else
                {
                    _key = stored;
                }
            }
            else
            {
                var key = RandomNumberGenerator.GetBytes(KeySize);
                var stored = OperatingSystem.IsWindows() ? DpapiProtect(key) : key;
                File.WriteAllBytes(_keyPath, stored);
                AppPaths.SecureFile(_keyPath);
                _key = key;
            }

            if (_key.Length != KeySize)
            {
                throw new CryptographicException("The token protection key has an invalid length.");
            }
            return _key;
        }
    }

    private static byte[] DpapiProtect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }
        return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
    }

    private static byte[] DpapiUnprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }
        return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
    }
}
