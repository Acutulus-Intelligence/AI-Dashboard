using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalDb.Services;

/// <summary>
/// Re-checks the host immediately before opening an external database connection.
/// The create/update path already validates hosts, but DNS can change afterwards
/// (rebinding), so the resolved address is evaluated again at use time.
/// </summary>
public sealed class ConnectionHostGuard
{
    private readonly ExternalDbSettings _settings;

    public ConnectionHostGuard(IOptions<ExternalDbSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task EnsureSafeAsync(
        DbProvider provider,
        string connectionString,
        CancellationToken ct = default)
    {
        if (!ConnectionStringParts.TryRead(
                provider,
                connectionString,
                out var host,
                out _,
                out _,
                out _,
                out _))
        {
            // Fail closed: if we cannot determine the host, we cannot re-check it.
            throw new InvalidOperationException("Connection string could not be read.");
        }

        if (await HostBlocklist.IsBlockedAsync(
                host,
                _settings.BlockedHosts,
                _settings.AllowPrivateHosts,
                _settings.AllowedHosts,
                ct))
        {
            throw new InvalidOperationException("This host is not allowed.");
        }
    }
}
