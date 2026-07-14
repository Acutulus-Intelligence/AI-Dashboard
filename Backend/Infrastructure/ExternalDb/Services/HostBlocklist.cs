using System.Net;
using System.Net.Sockets;

namespace Infrastructure.ExternalDb.Services;

public static class HostBlocklist
{
    public static bool IsBlocked(string host, IEnumerable<string> blockedHosts)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var blocked in blockedHosts)
        {
            if (normalized.Equals(blocked.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        }

        if (!IPAddress.TryParse(normalized, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
        }

        return false;
    }
}
