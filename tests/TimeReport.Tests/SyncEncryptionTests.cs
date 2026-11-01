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
    public void PassphraseDerivationIsStable()
    {
        Assert.Equal(
            SyncEncryptionService.DeriveKey("correct horse battery staple"),
            SyncEncryptionService.DeriveKey("correct horse battery staple"));
        Assert.NotEqual(
            SyncEncryptionService.DeriveKey("correct horse battery staple"),
            SyncEncryptionService.DeriveKey("different passphrase"));
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
