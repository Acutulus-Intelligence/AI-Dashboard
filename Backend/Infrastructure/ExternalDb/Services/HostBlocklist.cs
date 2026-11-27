using System.Net;
using System.Net.Sockets;

namespace Infrastructure.ExternalDb.Services;

/// <summary>
/// Blocks user-supplied database hosts that point at loopback, private, link-local
/// or otherwise reserved addresses. Hostnames are resolved first so a public name
/// cannot be used to reach an internal address, and resolution failures fail
/// closed. Multi-host connection strings are checked host by host so one blocked
/// entry cannot be hidden behind a public one. Private hosts can be enabled
/// explicitly for self-hosted deployments.
/// </summary>
public static class HostBlocklist
{
    public static async Task<bool> IsBlockedAsync(
        string host,
        IEnumerable<string> blockedHosts,
        bool allowPrivateHosts = false,
        IEnumerable<string>? allowedHosts = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        // PostgreSQL and MySQL allow comma-separated failover hosts. Every host
        // must pass; otherwise a private entry could hide behind a public one.
        var parts = host.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > 1)
        {
            foreach (var part in parts)
            {
                if (await IsBlockedAsync(part, blockedHosts, allowPrivateHosts, allowedHosts, ct))
                    return true;
            }

            return false;
        }

        var normalized = Normalize(host);
        var dnsHost = NormalizeForDns(normalized);

        if (blockedHosts.Any(b => Matches(b, normalized, dnsHost)))
            return true;

        if (allowedHosts?.Any(a => Matches(a, normalized, dnsHost)) == true)
            return false;

        if (IPAddress.TryParse(dnsHost, out var literal))
            return !allowPrivateHosts && IsPrivateOrReserved(literal);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(dnsHost, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail closed by default. An operator who explicitly allowed private
            // hosts may also use named instances or internal aliases that do not
            // resolve from the app server.
            return !allowPrivateHosts;
        }

        if (addresses.Length == 0)
            return !allowPrivateHosts;

        return !allowPrivateHosts && addresses.Any(IsPrivateOrReserved);
    }

    /// <summary>
    /// True for loopback, RFC1918, link-local, CGNAT, multicast, unspecified and
    /// IPv6 ULA/link-local addresses.
    /// </summary>
    public static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv4MappedToIPv6)
                return IsPrivateOrReserved(ip.MapToIPv4());

            if (ip.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(ip))
                return true;

            var v6 = ip.GetAddressBytes();
            if ((v6[0] & 0xFE) == 0xFC) // fc00::/7
                return true;
            if (v6[0] == 0xFE && (v6[1] & 0xC0) == 0x80) // fe80::/10
                return true;
            if (v6[0] == 0xFF) // ff00::/8 multicast
                return true;

            return false;
        }

        var b = ip.GetAddressBytes();

        if (b[0] == 0) // 0.0.0.0/8
            return true;
        if (b[0] == 10) // 10.0.0.0/8
            return true;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) // 100.64.0.0/10 CGNAT
            return true;
        if (b[0] == 127) // 127.0.0.0/8
            return true;
        if (b[0] == 169 && b[1] == 254) // 169.254.0.0/16 link-local
            return true;
        if (b[0] == 172 && b[1] is >= 16 and <= 31) // 172.16.0.0/12
            return true;
        if (b[0] == 192 && b[1] == 168) // 192.168.0.0/16
            return true;
        if (b[0] >= 224) // multicast, reserved, broadcast
            return true;

        return false;
    }

    private static bool Matches(string configured, string normalized, string dnsHost)
    {
        var candidate = Normalize(configured);
        return candidate == normalized || candidate == dnsHost;
    }

    /// <summary>
    /// Removes SQL Server transport/instance decorations so DNS sees a hostname:
    /// <c>tcp:host\INSTANCE,1433</c> becomes <c>host</c>. A comma-separated host
    /// list has already been split before this point.
    /// </summary>
    private static string NormalizeForDns(string host)
    {
        var candidate = host;

        if (candidate.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[4..];

        var instanceSeparator = candidate.IndexOf('\\');
        if (instanceSeparator > 0)
            candidate = candidate[..instanceSeparator];

        var portSeparator = candidate.IndexOf(',');
        if (portSeparator > 0)
            candidate = candidate[..portSeparator];

        if (candidate.Length > 1 && candidate[0] == '[' && candidate[^1] == ']')
            candidate = candidate[1..^1];

        return candidate.Trim();
    }

    private static string Normalize(string host) =>
        host.Trim().TrimEnd('.').ToLowerInvariant();
}
