using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TimeReport.Services;

public enum HttpClientPurpose
{
    Dropbox,
    Update,
}

public static class HttpClientFactory
{
    public static HttpClient Create(
        AppSettings settings,
        HttpClientPurpose purpose = HttpClientPurpose.Dropbox,
        TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(settings.DropboxProxyUrl))
        {
            if (TryValidateProxyUrl(settings.DropboxProxyUrl, out var containsCredentials))
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(new Uri(settings.DropboxProxyUrl.Trim()));
            }
            else
            {
                var reason = containsCredentials
                    ? "Ignoring proxy URL with embedded credentials"
                    : "Ignoring invalid proxy URL";
                AppLog.Warn($"{reason}: {RedactProxyUrl(settings.DropboxProxyUrl)}");
            }
        }

        // The custom CA is a Dropbox trust setting. Update checks use the system
        // trust store so a Dropbox-only enterprise CA cannot intercept GitHub.
        var caPath = settings.DropboxCaCertificatePath;
        if (purpose == HttpClientPurpose.Dropbox &&
            !string.IsNullOrWhiteSpace(caPath) &&
            File.Exists(caPath))
        {
            try
            {
                FileSafety.EnsureRegularFile(
                    caPath,
                    FileSafety.MaxCertificateBytes,
                    "custom CA certificate");
                handler.ServerCertificateCustomValidationCallback =
                    (_, certificate, _, errors) => ValidateWithCustomCa(certificate, errors, caPath);
            }
            catch (IOException exception)
            {
                AppLog.Warn(
                    $"Ignoring custom CA certificate file {Path.GetFileName(caPath)}.",
                    exception);
            }
        }

        return new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
        };
    }

    /// <summary>
    /// Validates that a proxy URL is an absolute HTTP(S) URL without embedded
    /// credentials. Credentials in proxy URLs leak into logs and settings dumps.
    /// </summary>
    public static bool TryValidateProxyUrl(string? value, out bool containsCredentials)
    {
        containsCredentials = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            containsCredentials = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Removes user information (and query/fragment data) before a proxy URL is
    /// written to the log.
    /// </summary>
    public static string RedactProxyUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return "<invalid>";
        }

        return uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped);
    }

    internal static bool ValidateWithCustomCa(
        X509Certificate2? certificate,
        SslPolicyErrors errors,
        string caPath)
    {
        if (certificate is null ||
            (errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0 ||
            (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            return false;
        }

        try
        {
            using var root = X509CertificateLoader.LoadCertificateFromFile(caPath);
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            // Require a TLS server authentication EKU even when the system trust
            // result was overridden by a custom enterprise root.
            chain.ChainPolicy.ApplicationPolicy.Add(
                new Oid("1.3.6.1.5.5.7.3.1"));
            // Internal enterprise roots often have no reachable revocation endpoint.
            // The user explicitly selected this CA for Dropbox; keep the build
            // deterministic instead of failing every request while offline.
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(certificate);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
