using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TimeReport.Services;

/// <summary>
/// Authenticated, chunked AES-GCM container used for Dropbox uploads.
/// Version 2 authenticates a per-file identifier, chunk index, chunk length and
/// final marker. Version 1 files remain readable for backward compatibility.
/// </summary>
public static class SyncEncryptionService
{
    private static readonly byte[] MagicV1 = "TRSYNC1\0"u8.ToArray();
    private static readonly byte[] MagicV2 = "TRSYNC2\0"u8.ToArray();
    private const int FileIdSize = 16;
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
        Span<byte> header = stackalloc byte[8];
        return stream.Read(header) == header.Length &&
               (header.SequenceEqual(MagicV1) || header.SequenceEqual(MagicV2));
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
        destination.Write(MagicV2);
        var fileId = RandomNumberGenerator.GetBytes(FileIdSize);
        destination.Write(fileId);

        var plain = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var lengthBuffer = new byte[4];
        using var aes = new AesGcm(keyBytes, TagSize);

        uint chunkIndex = 0;
        int read;
        while ((read = source.Read(plain, 0, plain.Length)) > 0)
        {
            RandomNumberGenerator.Fill(nonce);
            var associatedData = BuildAssociatedData(
                MagicV2,
                fileId,
                chunkIndex,
                read,
                final: false);
            aes.Encrypt(
                nonce,
                plain.AsSpan(0, read),
                cipher.AsSpan(0, read),
                tag,
                associatedData);
            WriteRecord(destination, lengthBuffer, read, nonce, tag, cipher.AsSpan(0, read));
            chunkIndex++;
        }

        RandomNumberGenerator.Fill(nonce);
        var finalAssociatedData = BuildAssociatedData(
            MagicV2,
            fileId,
            chunkIndex,
            0,
            final: true);
        aes.Encrypt(
            nonce,
            ReadOnlySpan<byte>.Empty,
            Span<byte>.Empty,
            tag,
            finalAssociatedData);
        WriteRecord(destination, lengthBuffer, 0, nonce, tag, ReadOnlySpan<byte>.Empty);

        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    public static void DecryptFile(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = DecodeKey(key);
        using var source = File.OpenRead(sourcePath);
        Span<byte> header = stackalloc byte[8];
        ReadExactly(source, header);

        using var destination = File.Create(destinationPath);
        if (header.SequenceEqual(MagicV2))
        {
            DecryptVersion2(source, destination, keyBytes);
        }
        else if (header.SequenceEqual(MagicV1))
        {
            DecryptVersion1(source, destination, keyBytes);
        }
        else
        {
            throw new CryptographicException("The sync file is not a TimeReport encrypted container.");
        }

        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    private static void DecryptVersion2(Stream source, Stream destination, byte[] keyBytes)
    {
        var fileId = new byte[FileIdSize];
        ReadExactly(source, fileId);

        var plain = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var lengthBuffer = new byte[4];
        using var aes = new AesGcm(keyBytes, TagSize);

        uint chunkIndex = 0;
        while (true)
        {
            ReadExactly(source, lengthBuffer);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (length < 0 || length > ChunkSize)
            {
                throw new CryptographicException("The sync file contains an invalid chunk length.");
            }

            ReadExactly(source, nonce);
            ReadExactly(source, tag);
            if (length == 0)
            {
                var finalAssociatedData = BuildAssociatedData(
                    MagicV2,
                    fileId,
                    chunkIndex,
                    0,
                    final: true);
                aes.Decrypt(
                    nonce,
                    ReadOnlySpan<byte>.Empty,
                    tag,
                    Span<byte>.Empty,
                    finalAssociatedData);
                if (source.ReadByte() != -1)
                {
                    throw new CryptographicException("The sync file contains trailing data.");
                }
                break;
            }

            ReadExactly(source, cipher.AsSpan(0, length));
            var associatedData = BuildAssociatedData(
                MagicV2,
                fileId,
                chunkIndex,
                length,
                final: false);
            aes.Decrypt(
                nonce,
                cipher.AsSpan(0, length),
                tag,
                plain.AsSpan(0, length),
                associatedData);
            destination.Write(plain, 0, length);
            chunkIndex++;
        }
    }

    private static void DecryptVersion1(Stream source, Stream destination, byte[] keyBytes)
    {
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
    }

    private static void WriteRecord(
        Stream destination,
        byte[] lengthBuffer,
        int length,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> cipher)
    {
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, length);
        destination.Write(lengthBuffer);
        destination.Write(nonce);
        destination.Write(tag);
        destination.Write(cipher);
    }

    private static byte[] BuildAssociatedData(
        byte[] magic,
        byte[] fileId,
        uint chunkIndex,
        int length,
        bool final)
    {
        var associatedData = new byte[magic.Length + fileId.Length + 4 + 4 + 1];
        magic.CopyTo(associatedData, 0);
        fileId.CopyTo(associatedData, magic.Length);
        var offset = magic.Length + fileId.Length;
        BinaryPrimitives.WriteUInt32BigEndian(
            associatedData.AsSpan(offset, 4),
            chunkIndex);
        BinaryPrimitives.WriteInt32BigEndian(
            associatedData.AsSpan(offset + 4, 4),
            length);
        associatedData[^1] = final ? (byte)1 : (byte)0;
        return associatedData;
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
