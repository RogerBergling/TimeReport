using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TimeReport.Services;

public enum SyncContainerVersion
{
    None = 0,
    Version1 = 1,
    Version2 = 2,
    Version3 = 3,
}

/// <summary>
/// Authenticated, chunked AES-GCM container used for Dropbox uploads.
/// Version 3 derives a per-file key from the sync passphrase with PBKDF2 and a
/// random salt stored in the container. Version 1 and 2 files remain readable
/// with their legacy derived key.
/// </summary>
public static class SyncEncryptionService
{
    private static readonly byte[] MagicV1 = "TRSYNC1\0"u8.ToArray();
    private static readonly byte[] MagicV2 = "TRSYNC2\0"u8.ToArray();
    private static readonly byte[] MagicV3 = "TRSYNC3\0"u8.ToArray();
    private const int FileIdSize = 16;
    private const int SaltSize = 16;
    private const int ChunkSize = 1024 * 1024;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int LegacyPbkdf2Iterations = 200_000;
    private const int Pbkdf2Iterations = 600_000;
    private const int MinimumPassphraseLength = 12;
    private const int MinimumDistinctCharacters = 6;

    public static SyncContainerVersion GetContainerVersion(string path)
    {
        if (!PathSafety.IsRegularFile(path))
        {
            return SyncContainerVersion.None;
        }

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[8];
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            if (read != header.Length)
            {
                return SyncContainerVersion.None;
            }
            if (header.SequenceEqual(MagicV1))
            {
                return SyncContainerVersion.Version1;
            }
            if (header.SequenceEqual(MagicV2))
            {
                return SyncContainerVersion.Version2;
            }
            if (header.SequenceEqual(MagicV3))
            {
                return SyncContainerVersion.Version3;
            }
            return SyncContainerVersion.None;
        }
        catch (IOException)
        {
            return SyncContainerVersion.None;
        }
        catch (UnauthorizedAccessException)
        {
            return SyncContainerVersion.None;
        }
    }

    public static bool IsEncryptedFile(string path) =>
        GetContainerVersion(path) != SyncContainerVersion.None;

    public static string GenerateKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Legacy version 1/2 key derivation with a fixed salt. Kept only so existing
    /// settings and containers continue to work. New passphrase containers use a
    /// random per-file salt.
    /// </summary>
    public static string DeriveKey(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Enter a sync passphrase.", nameof(passphrase));
        }
        var salt = Encoding.UTF8.GetBytes("TimeReport sync encryption v1");
        return Convert.ToBase64String(
            DeriveKeyBytes(passphrase, salt, LegacyPbkdf2Iterations));
    }

    public static bool IsPassphraseAcceptable(string? passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            return false;
        }

        var value = passphrase.Trim();
        if (value.Length < MinimumPassphraseLength ||
            value.Distinct().Count() < MinimumDistinctCharacters)
        {
            return false;
        }

        if (value.Length < 16)
        {
            var categories = 0;
            if (value.Any(char.IsLower))
            {
                categories++;
            }
            if (value.Any(char.IsUpper))
            {
                categories++;
            }
            if (value.Any(char.IsDigit))
            {
                categories++;
            }
            if (value.Any(character => !char.IsLetterOrDigit(character)))
            {
                categories++;
            }
            if (categories < 3)
            {
                return false;
            }
        }

        return true;
    }

    public static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Enter a sync passphrase.", nameof(passphrase));
        }
        if (!IsPassphraseAcceptable(passphrase))
        {
            throw new ArgumentException(
                "Use a sync passphrase with at least 12 characters and more variety. " +
                "A longer multi-word passphrase is recommended.");
        }
    }

    public static void EncryptFile(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = DecodeKey(key);
        using var source = OpenSource(sourcePath);
        using var destination = AppPaths.CreatePrivateFileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        EncryptFileCore(source, destination, MagicV2, salt: null, keyBytes);
        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    public static void EncryptFileWithPassphrase(
        string sourcePath,
        string destinationPath,
        string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Enter a sync passphrase.", nameof(passphrase));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var keyBytes = DeriveKeyBytes(passphrase, salt, Pbkdf2Iterations);
        using var source = OpenSource(sourcePath);
        using var destination = AppPaths.CreatePrivateFileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        EncryptFileCore(source, destination, MagicV3, salt, keyBytes);
        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    public static void DecryptFile(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = DecodeKey(key);
        using var source = OpenSource(sourcePath);
        Span<byte> header = stackalloc byte[8];
        ReadExactly(source, header);

        using var destination = AppPaths.CreatePrivateFileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        if (header.SequenceEqual(MagicV2))
        {
            var fileId = new byte[FileIdSize];
            ReadExactly(source, fileId);
            DecryptVersion2Records(source, destination, keyBytes, MagicV2, fileId);
        }
        else if (header.SequenceEqual(MagicV1))
        {
            DecryptVersion1(source, destination, keyBytes);
        }
        else if (header.SequenceEqual(MagicV3))
        {
            throw new CryptographicException(
                "The sync file uses version 3 encryption. Enter the sync passphrase used on the device that uploaded it.");
        }
        else
        {
            throw new CryptographicException("The sync file is not a TimeReport encrypted container.");
        }

        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    public static void DecryptFileWithPassphrase(
        string sourcePath,
        string destinationPath,
        string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Enter a sync passphrase.", nameof(passphrase));
        }

        using var source = OpenSource(sourcePath);
        Span<byte> header = stackalloc byte[8];
        ReadExactly(source, header);
        if (!header.SequenceEqual(MagicV3))
        {
            throw new CryptographicException(
                "The sync file is not a version 3 TimeReport encrypted container.");
        }

        var salt = new byte[SaltSize];
        ReadExactly(source, salt);
        var fileId = new byte[FileIdSize];
        ReadExactly(source, fileId);
        var keyBytes = DeriveKeyBytes(passphrase, salt, Pbkdf2Iterations);

        using var destination = AppPaths.CreatePrivateFileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        DecryptVersion2Records(source, destination, keyBytes, MagicV3, fileId);
        destination.Flush();
        AppPaths.SecureFile(destinationPath);
    }

    private static void EncryptFileCore(
        Stream source,
        Stream destination,
        byte[] magic,
        byte[]? salt,
        byte[] keyBytes)
    {
        destination.Write(magic);
        if (salt is not null)
        {
            destination.Write(salt);
        }
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
                magic,
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
            magic,
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
    }

    private static void DecryptVersion2Records(
        Stream source,
        Stream destination,
        byte[] keyBytes,
        byte[] magic,
        byte[] fileId)
    {
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
                    magic,
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
                magic,
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

    private static byte[] DeriveKeyBytes(
        string passphrase,
        byte[] salt,
        int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

    private static FileStream OpenSource(string path)
    {
        try
        {
            FileSafety.EnsureRegularFile(
                path,
                FileSafety.MaxDatabaseBytes,
                "sync file");
        }
        catch (IOException exception)
        {
            throw new CryptographicException(exception.Message, exception);
        }

        return File.OpenRead(path);
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
