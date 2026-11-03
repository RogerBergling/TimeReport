using System.Net;
using System.Net.Security;
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
            if (Uri.TryCreate(settings.DropboxProxyUrl.Trim(), UriKind.Absolute, out var proxyUri))
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(proxyUri);
            }
            else
            {
                AppLog.Warn($"Ignoring invalid proxy URL: {settings.DropboxProxyUrl}");
            }
        }

        // The custom CA is a Dropbox trust setting. Update checks use the system
        // trust store so a Dropbox-only enterprise CA cannot intercept GitHub.
        var caPath = settings.DropboxCaCertificatePath;
        if (purpose == HttpClientPurpose.Dropbox &&
            !string.IsNullOrWhiteSpace(caPath) &&
            File.Exists(caPath))
        {
            handler.ServerCertificateCustomValidationCallback =
                (_, certificate, _, errors) => ValidateWithCustomCa(certificate, errors, caPath);
        }

        return new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
        };
    }

    private static bool ValidateWithCustomCa(
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
