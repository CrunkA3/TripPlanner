using System.Net;
using System.Net.Sockets;

namespace TripPlanner.Web.Services;

/// <summary>
/// Provides helpers for validating user-supplied URLs before server-side fetching
/// to protect against Server-Side Request Forgery (SSRF).
/// </summary>
public static class UrlSecurityHelper
{
    /// <summary>
    /// Returns <c>true</c> if the URI's host resolves to a loopback, private, or
    /// link-local address that must not be fetched by the server.
    /// Note: this check is performed on the literal host value before DNS resolution;
    /// DNS-rebinding attacks require additional network-layer mitigations.
    /// </summary>
    public static bool IsPrivateOrLocalUri(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        // Block explicit loopback / unspecified hostnames
        if (host is "localhost" or "0.0.0.0")
            return true;

        // Try to parse the host as a raw IP address
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                // 10.0.0.0/8
                if (b[0] == 10) return true;
                // 127.0.0.0/8 (loopback range)
                if (b[0] == 127) return true;
                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;
                // 169.254.0.0/16 (link-local / cloud metadata endpoints)
                if (b[0] == 169 && b[1] == 254) return true;
                // 0.0.0.0/8
                if (b[0] == 0) return true;
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                    return true;
            }
        }

        return false;
    }
}
