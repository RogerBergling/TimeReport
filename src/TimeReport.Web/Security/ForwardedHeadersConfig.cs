using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace TimeReport.Web.Security;

public static class ForwardedHeadersConfig
{
    public static void Configure(ForwardedHeadersOptions options, string trustedProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var added = false;
        foreach (var raw in trustedProxies.Split(
                     [',', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Contains('/', StringComparison.Ordinal))
            {
                var parts = raw.Split('/', 2);
                if (IPAddress.TryParse(parts[0], out var address) &&
                    int.TryParse(parts[1], out var prefix) &&
                    prefix >= 0 &&
                    prefix <= (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(address, prefix));
                    added = true;
                }
            }
            else if (IPAddress.TryParse(raw, out var proxy))
            {
                options.KnownProxies.Add(proxy);
                added = true;
            }
        }

        if (!added)
        {
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        }
    }
}
