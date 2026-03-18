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
    /// Returns <c>true</c> if the URI's host is a literal IP address (or one of the
    /// well-known loopback hostnames "localhost" / "0.0.0.0") that falls within a
    /// loopback, private, or link-local range.
    /// <para>
    /// <b>Important:</b> This check operates on the literal host value and does
    /// <b>not</b> perform DNS resolution. URLs with hostnames that resolve to private
    /// IP addresses (e.g. <c>internal.corp</c>) are <b>not</b> blocked here; those
    /// must be handled at the network or firewall level. DNS-rebinding attacks
    /// similarly require network-layer mitigations beyond this check.
    /// </para>
    /// </summary>
    public static bool IsPrivateOrLocalUri(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        // Block well-known loopback / unspecified hostnames
        if (host is "localhost" or "0.0.0.0")
            return true;

        // Only continue if the host is a raw IP address literal
        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return IsPrivateOrLocalIPv4(ip);

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // IPv4-mapped IPv6 (::ffff:x.x.x.x) — evaluate the embedded IPv4 address
            if (ip.IsIPv4MappedToIPv6)
                return IsPrivateOrLocalIPv4(ip.MapToIPv4());

            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;

            // Unique Local Addresses (fc00::/7, i.e. fc__ and fd__)
            var b6 = ip.GetAddressBytes();
            if ((b6[0] & 0xFE) == 0xFC)
                return true;
        }

        return false;
    }

    private static bool IsPrivateOrLocalIPv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        // 0.0.0.0/8
        if (b[0] == 0) return true;
        // 10.0.0.0/8
        if (b[0] == 10) return true;
        // 127.0.0.0/8 (loopback range)
        if (b[0] == 127) return true;
        // 169.254.0.0/16 (link-local / cloud metadata endpoints)
        if (b[0] == 169 && b[1] == 254) return true;
        // 172.16.0.0/12
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        // 192.168.0.0/16
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }
}
