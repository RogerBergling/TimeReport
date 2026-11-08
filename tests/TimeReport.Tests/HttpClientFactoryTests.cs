using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class HttpClientFactoryTests
{
    [Fact]
    public void AcceptsPlainHttpAndHttpsProxyUrls()
    {
        Assert.True(HttpClientFactory.TryValidateProxyUrl("http://proxy.example:8080", out var httpCredentials));
        Assert.False(httpCredentials);
        Assert.True(HttpClientFactory.TryValidateProxyUrl("https://proxy.example", out var httpsCredentials));
        Assert.False(httpsCredentials);
        Assert.True(HttpClientFactory.TryValidateProxyUrl(null, out _));
    }

    [Fact]
    public void RejectsCredentialBearingProxyUrl()
    {
        var allowed = HttpClientFactory.TryValidateProxyUrl(
            "http://user:secret@proxy.example:8080",
            out var containsCredentials);

        Assert.False(allowed);
        Assert.True(containsCredentials);
    }

    [Theory]
    [InlineData("not a URL")]
    [InlineData("ftp://proxy.example")]
    [InlineData("http:///missing-host")]
    public void RejectsInvalidProxyUrl(string value)
    {
        Assert.False(HttpClientFactory.TryValidateProxyUrl(value, out _));
        Assert.Equal("<invalid>", HttpClientFactory.RedactProxyUrl(value));
    }

    [Fact]
    public void RedactsCredentialsBeforeLogging()
    {
        var redacted = HttpClientFactory.RedactProxyUrl(
            "http://user:secret@proxy.example:8080/path?token=abc");

        Assert.DoesNotContain("user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token", redacted, StringComparison.Ordinal);
        Assert.Equal("http://proxy.example:8080/path", redacted);
    }

    [Fact]
    public void CustomCaAcceptsServerAuthenticationCertificate()
    {
        using var directory = new TempDirectory();
        using var rootKey = RSA.Create(2048);
        using var root = CreateRootCertificate(rootKey);
        using var serverKey = RSA.Create(2048);
        using var server = CreateServerCertificate(
            root,
            serverKey,
            "1.3.6.1.5.5.7.3.1");
        var caPath = Path.Combine(directory.Path, "ca.pem");
        File.WriteAllText(caPath, root.ExportCertificatePem());

        Assert.True(HttpClientFactory.ValidateWithCustomCa(
            server,
            SslPolicyErrors.None,
            caPath));
    }

    [Fact]
    public void CustomCaRejectsCertificateWithoutServerAuthenticationEku()
    {
        using var directory = new TempDirectory();
        using var rootKey = RSA.Create(2048);
        using var root = CreateRootCertificate(rootKey);
        using var serverKey = RSA.Create(2048);
        using var server = CreateServerCertificate(
            root,
            serverKey,
            "1.3.6.1.5.5.7.3.2");
        var caPath = Path.Combine(directory.Path, "ca.pem");
        File.WriteAllText(caPath, root.ExportCertificatePem());

        Assert.False(HttpClientFactory.ValidateWithCustomCa(
            server,
            SslPolicyErrors.None,
            caPath));
    }

    private static X509Certificate2 CreateRootCertificate(RSA key)
    {
        var request = new CertificateRequest(
            "CN=TimeReport Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
    }

    private static X509Certificate2 CreateServerCertificate(
        X509Certificate2 issuer,
        RSA key,
        string enhancedKeyUsageOid)
    {
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(enhancedKeyUsageOid) },
                false));
        return request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30),
            RandomNumberGenerator.GetBytes(16));
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
