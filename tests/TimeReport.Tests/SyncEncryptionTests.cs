using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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

    [Fact]
    public void LegacyDerivedKeyKeepsOriginalIterationCount()
    {
        var expected = Convert.ToBase64String(
            Rfc2898DeriveBytes.Pbkdf2(
                "correct horse battery staple",
                Encoding.UTF8.GetBytes("TimeReport sync encryption v1"),
                200_000,
                HashAlgorithmName.SHA256,
                32));

        Assert.Equal(expected, SyncEncryptionService.DeriveKey("correct horse battery staple"));
    }

    [Fact]
    public void VersionThreeUsesRandomSaltAndPassphrase()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var first = Path.Combine(directory.Path, "first.enc");
        var second = Path.Combine(directory.Path, "second.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        var data = new byte[1024 * 1024 + 33];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(source, data);
        const string passphrase = "correct horse battery staple";

        SyncEncryptionService.EncryptFileWithPassphrase(source, first, passphrase);
        SyncEncryptionService.EncryptFileWithPassphrase(source, second, passphrase);

        var firstBytes = File.ReadAllBytes(first);
        var secondBytes = File.ReadAllBytes(second);
        Assert.Equal("TRSYNC3\0"u8.ToArray(), firstBytes[..8]);
        Assert.Equal(SyncContainerVersion.Version3, SyncEncryptionService.GetContainerVersion(first));
        Assert.NotEqual(
            firstBytes.AsSpan(8, 16).ToArray(),
            secondBytes.AsSpan(8, 16).ToArray());

        SyncEncryptionService.DecryptFileWithPassphrase(first, decrypted, passphrase);
        Assert.Equal(data, File.ReadAllBytes(decrypted));
    }

    [Fact]
    public void VersionThreeRejectsWrongPassphrase()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        File.WriteAllText(source, "secret");
        SyncEncryptionService.EncryptFileWithPassphrase(
            source,
            encrypted,
            "correct horse battery staple");

        Assert.ThrowsAny<CryptographicException>(() =>
            SyncEncryptionService.DecryptFileWithPassphrase(
                encrypted,
                decrypted,
                "wrong passphrase that is long enough"));
    }

    [Fact]
    public void LegacyDecryptRejectsVersionThreeWithClearMessage()
    {
        using var directory = new TempDirectory();
        var source = Path.Combine(directory.Path, "source.db");
        var encrypted = Path.Combine(directory.Path, "source.enc");
        var decrypted = Path.Combine(directory.Path, "decrypted.db");
        File.WriteAllText(source, "secret");
        SyncEncryptionService.EncryptFileWithPassphrase(
            source,
            encrypted,
            "correct horse battery staple");

        var exception = Assert.ThrowsAny<CryptographicException>(() =>
            SyncEncryptionService.DecryptFile(
                encrypted,
                decrypted,
                SyncEncryptionService.GenerateKey()));
        Assert.Contains("passphrase", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("aaaaaaaaaaaa", false)]
    [InlineData("123456789012", false)]
    [InlineData("abcdefghijkl", false)]
    [InlineData("correct horse battery staple", true)]
    [InlineData("Longer-Passphrase-9", true)]
    public void PassphrasePolicyRequiresLengthAndVariety(string passphrase, bool expected)
    {
        Assert.Equal(expected, SyncEncryptionService.IsPassphraseAcceptable(passphrase));
        if (expected)
        {
            SyncEncryptionService.ValidatePassphrase(passphrase);
        }
        else
        {
            Assert.Throws<ArgumentException>(() =>
                SyncEncryptionService.ValidatePassphrase(passphrase));
        }
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
