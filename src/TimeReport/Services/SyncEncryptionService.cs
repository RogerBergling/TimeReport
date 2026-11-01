using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TimeReport.Services;

/// <summary>
/// Authenticated, chunked AES-GCM container used for Dropbox uploads.
/// The remote file never contains a plaintext SQLite database.
/// </summary>
public static class SyncEncryptionService
{
    private static readonly byte[] Magic = "TRSYNC1\0"u8.ToArray();
    private const int ChunkSize = 1024 * 1024;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static bool IsEncryptedFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[Magic.Length];
        return stream.Read(header) == header.Length && header.SequenceEqual(Magic);
    }

    public static string GenerateKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string DeriveKey(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Enter a sync passphrase.", nameof(passphrase));
        }
        var salt = Encoding.UTF8.GetBytes("TimeReport sync encryption v1");
        var key = Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            200_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(key);
    }

    public static void EncryptFile(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = DecodeKey(key);
        using var source = File.OpenRead(sourcePath);
        using var destination = File.Create(destinationPath);
        destination.Write(Magic);

        var plain = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var lengthBuffer = new byte[4];
        using var aes = new AesGcm(keyBytes, TagSize);

        int read;
        while ((read = source.Read(plain, 0, plain.Length)) > 0)
        {
            RandomNumberGenerator.Fill(nonce);
            aes.Encrypt(nonce, plain.AsSpan(0, read), cipher.AsSpan(0, read), tag);
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, read);
            destination.Write(lengthBuffer);
            destination.Write(nonce);
            destination.Write(tag);
            destination.Write(cipher, 0, read);
        }

        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, 0);
        destination.Write(lengthBuffer);
        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    public static void DecryptFile(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = DecodeKey(key);
        using var source = File.OpenRead(sourcePath);
        Span<byte> header = stackalloc byte[Magic.Length];
        if (source.Read(header) != header.Length || !header.SequenceEqual(Magic))
        {
            throw new CryptographicException("The sync file is not a TimeReport encrypted container.");
        }

        using var destination = File.Create(destinationPath);
        var plain = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var lengthBuffer = new byte[4];
        using var aes = new AesGcm(keyBytes, TagSize);

        while (true)
        {
            ReadExactly(source, lengthBuffer);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (length == 0)
            {
                break;
            }
            if (length < 0 || length > ChunkSize)
            {
                throw new CryptographicException("The sync file contains an invalid chunk length.");
            }

            ReadExactly(source, nonce);
            ReadExactly(source, tag);
            ReadExactly(source, cipher.AsSpan(0, length));
            aes.Decrypt(nonce, cipher.AsSpan(0, length), tag, plain.AsSpan(0, length));
            destination.Write(plain, 0, length);
        }

        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    private static byte[] DecodeKey(string key)
    {
        var bytes = Convert.FromBase64String(key);
        if (bytes.Length != 32)
        {
            throw new CryptographicException("The sync encryption key must be 32 bytes.");
        }
        return bytes;
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new CryptographicException("The sync file is truncated.");
            }
            offset += read;
        }
    }
}
