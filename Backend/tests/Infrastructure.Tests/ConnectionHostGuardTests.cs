using Domain.Enums;
using FluentAssertions;
using Infrastructure.ExternalDb;
using Infrastructure.ExternalDb.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests;

public sealed class ConnectionHostGuardTests
{
    private static ConnectionHostGuard Guard(bool allowPrivateHosts) =>
        new(Options.Create(new ExternalDbSettings
        {
            AllowPrivateHosts = allowPrivateHosts,
        }));

    [Fact]
    public async Task Rejects_private_host_when_not_allowed()
    {
        var guard = Guard(allowPrivateHosts: false);

        var act = () => guard.EnsureSafeAsync(
            DbProvider.PostgreSql,
            "Host=127.0.0.1;Port=5432;Database=x;Username=u;Password=p",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Allows_private_host_when_enabled()
    {
        var guard = Guard(allowPrivateHosts: true);

        await guard.EnsureSafeAsync(
            DbProvider.PostgreSql,
            "Host=127.0.0.1;Port=5432;Database=x;Username=u;Password=p",
            CancellationToken.None);
    }

    [Fact]
    public async Task Rejects_multi_host_when_any_part_is_private()
    {
        var guard = Guard(allowPrivateHosts: false);

        var act = () => guard.EnsureSafeAsync(
            DbProvider.PostgreSql,
            "Host=8.8.8.8,127.0.0.1;Port=5432;Database=x;Username=u;Password=p",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rejects_an_unreadable_connection_string()
    {
        var guard = Guard(allowPrivateHosts: true);

        var act = () => guard.EnsureSafeAsync(
            DbProvider.PostgreSql,
            "not a connection string",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
