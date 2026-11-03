using System.Buffers.Binary;
using System.Security.Cryptography;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class SyncEncryptionTests
{
    [Fact]
    public void EncryptsAndDecryptsChunkedFile()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        var data = new byte[3 * 1024 * 1024 + 17];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(source, data);
        var key = SyncEncryptionService.GenerateKey();

        SyncEncryptionService.EncryptFile(source, encrypted, key);

        Assert.True(SyncEncryptionService.IsEncryptedFile(encrypted));
        Assert.DoesNotContain("SQLite format 3", File.ReadAllText(encrypted));

        SyncEncryptionService.DecryptFile(encrypted, decrypted, key);
        Assert.Equal(data, File.ReadAllBytes(decrypted));
    }

    [Fact]
    public void WrongKeyIsRejected()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        File.WriteAllText(source, "secret");
        SyncEncryptionService.EncryptFile(
            source,
            encrypted,
            SyncEncryptionService.GenerateKey());

        Assert.ThrowsAny<CryptographicException>(() =>
            SyncEncryptionService.DecryptFile(
                encrypted,
                decrypted,
                SyncEncryptionService.GenerateKey()));
    }

    [Fact]
    public void EncryptedFileUsesVersionTwoContainer()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        File.WriteAllText(source, "data");
        SyncEncryptionService.EncryptFile(
            source,
            encrypted,
            SyncEncryptionService.GenerateKey());

        var bytes = File.ReadAllBytes(encrypted);
        Assert.Equal("TRSYNC2\0"u8.ToArray(), bytes[..8]);
    }

    [Fact]
    public void VersionOneContainerStillDecrypts()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        var data = new byte[3 * 1024 * 1024 + 17];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(source, data);
        var key = SyncEncryptionService.GenerateKey();

        EncryptVersionOne(source, encrypted, key);

        Assert.True(SyncEncryptionService.IsEncryptedFile(encrypted));
        SyncEncryptionService.DecryptFile(encrypted, decrypted, key);
        Assert.Equal(data, File.ReadAllBytes(decrypted));
    }

    [Fact]
    public void TruncatedContainerIsRejected()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        var data = new byte[2 * 1024 * 1024 + 17];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(source, data);
        var key = SyncEncryptionService.GenerateKey();
        SyncEncryptionService.EncryptFile(source, encrypted, key);

        var bytes = File.ReadAllBytes(encrypted);
        File.WriteAllBytes(encrypted, bytes[..^8]);

        Assert.ThrowsAny<CryptographicException>(() =>
            SyncEncryptionService.DecryptFile(encrypted, decrypted, key));
    }

    [Fact]
    public void ReorderedChunksAreRejected()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.db.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        var data = new byte[2 * 1024 * 1024 + 17];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(source, data);
        var key = SyncEncryptionService.GenerateKey();
        SyncEncryptionService.EncryptFile(source, encrypted, key);

        File.WriteAllBytes(encrypted, SwapFirstTwoVersionTwoChunks(File.ReadAllBytes(encrypted)));

        Assert.ThrowsAny<CryptographicException>(() =>
            SyncEncryptionService.DecryptFile(encrypted, decrypted, key));
    }

    [Fact]
    public void PassphraseDerivationIsStable()
    {
        Assert.Equal(
            SyncEncryptionService.DeriveKey("correct horse battery staple"),
            SyncEncryptionService.DeriveKey("correct horse battery staple"));
        Assert.NotEqual(
            SyncEncryptionService.DeriveKey("correct horse battery staple"),
            SyncEncryptionService.DeriveKey("different passphrase"));
    }

    private static void EncryptVersionOne(string sourcePath, string destinationPath, string key)
    {
        var keyBytes = Convert.FromBase64String(key);
        using var source = File.OpenRead(sourcePath);
        using var destination = File.Create(destinationPath);
        destination.Write("TRSYNC1\0"u8);

        var plain = new byte[1024 * 1024];
        var cipher = new byte[plain.Length];
        var nonce = new byte[12];
        var tag = new byte[16];
        var lengthBuffer = new byte[4];
        using var aes = new AesGcm(keyBytes, 16);

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
    }

    private static byte[] SwapFirstTwoVersionTwoChunks(byte[] container)
    {
        var offset = 8 + 16;
        var first = ReadVersionTwoRecord(container, ref offset);
        var second = ReadVersionTwoRecord(container, ref offset);
        var result = new byte[container.Length];
        var target = 0;
        container.AsSpan(0, 8 + 16).CopyTo(result);
        target += 8 + 16;
        second.CopyTo(result, target);
        target += second.Length;
        first.CopyTo(result, target);
        target += first.Length;
        container.AsSpan(offset).CopyTo(result.AsSpan(target));
        return result;
    }

    private static byte[] ReadVersionTwoRecord(byte[] container, ref int offset)
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(container.AsSpan(offset, 4));
        var size = 4 + 12 + 16 + Math.Max(0, length);
        var record = container.AsSpan(offset, size).ToArray();
        offset += size;
        return record;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
