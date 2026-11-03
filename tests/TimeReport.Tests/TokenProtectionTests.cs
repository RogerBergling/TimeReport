using System.Security.Cryptography;
using System.Text.Json;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class TokenProtectionTests
{
    [Fact]
    public void ProtectsAndUnprotectsTokens()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        TokenProtection.Initialize(settingsPath);

        var protectedValue = TokenProtection.Protect("refresh-token");

        Assert.NotNull(protectedValue);
        Assert.StartsWith("enc:v1:", protectedValue);
        Assert.NotEqual("refresh-token", protectedValue);
        Assert.Equal("refresh-token", TokenProtection.Unprotect(protectedValue));
    }

    [Fact]
    public void SettingsFileDoesNotContainPlaintextTokens()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            DropboxAppSecret = "app-secret",
            DropboxRefreshToken = "refresh-token",
            DropboxAccessToken = "access-token",
        };

        settings.Save(settingsPath);

        var json = File.ReadAllText(settingsPath);
        Assert.DoesNotContain("app-secret", json);
        Assert.DoesNotContain("refresh-token", json);
        Assert.DoesNotContain("access-token", json);
        Assert.Contains("enc:v1:", json);
        Assert.True(File.Exists(settingsPath + ".key"));
    }

    [Fact]
    public void LegacyPlaintextSettingsAreMigrated()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var legacy = new
        {
            DropboxAppSecret = "legacy-secret",
            DropboxRefreshToken = "legacy-refresh",
            DropboxAccessToken = "legacy-access",
        };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(legacy));

        var loaded = AppSettings.Load(settingsPath);

        Assert.Equal("legacy-secret", loaded.DropboxAppSecret);
        Assert.Equal("legacy-refresh", loaded.DropboxRefreshToken);
        Assert.Equal("legacy-access", loaded.DropboxAccessToken);
        var stored = File.ReadAllText(settingsPath);
        Assert.DoesNotContain("legacy-secret", stored);
        Assert.Contains("enc:v1:", stored);
    }

    [Fact]
    public void UnprotectWithoutKeyFileThrows()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        TokenProtection.Initialize(settingsPath);
        var protectedValue = TokenProtection.Protect("secret");
        File.Delete(settingsPath + ".key");

        // Clear the process-wide key cache so this behaves like a fresh start.
        TokenProtection.Initialize(Path.Combine(directory.Path, "other.json"));

        Assert.Throws<CryptographicException>(() => TokenProtection.Unprotect(protectedValue));
    }

    [Fact]
    public void CorruptSettingsAreRejectedWithoutOverwritingTheFile()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        const string corrupt = "{ not valid json";
        File.WriteAllText(settingsPath, corrupt);

        Assert.Throws<SettingsException>(() => AppSettings.Load(settingsPath));
        Assert.Equal(corrupt, File.ReadAllText(settingsPath));
    }

    [Fact]
    public void MissingKeyFileIsRejectedInsteadOfCreatingANewKey()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            DatabaseEncryptionKey = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32)),
        };
        settings.Save(settingsPath);
        File.Delete(settingsPath + ".key");

        // Clear the process-wide key cache so this behaves like a fresh start.
        TokenProtection.Initialize(Path.Combine(directory.Path, "other.json"));

        Assert.Throws<SettingsException>(() => AppSettings.Load(settingsPath));
        Assert.False(File.Exists(settingsPath + ".key"));
    }

    [Fact]
    public void MissingSettingsFileWithExistingKeyFileIsRejected()
    {
        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(settingsPath + ".key", "orphan");

        Assert.Throws<SettingsException>(() => AppSettings.Load(settingsPath));
    }

    [Fact]
    public void KeyFileIsOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        TokenProtection.Initialize(settingsPath);
        TokenProtection.Protect("token");

        var mode = File.GetUnixFileMode(settingsPath + ".key");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
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
