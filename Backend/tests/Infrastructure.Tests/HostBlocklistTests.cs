using FluentAssertions;
using Infrastructure.ExternalDb.Services;

namespace Infrastructure.Tests;

public sealed class HostBlocklistTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.10.20.30")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:192.168.1.10")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    public async Task Blocks_private_and_reserved_addresses(string host)
    {
        (await HostBlocklist.IsBlockedAsync(host, [])).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public async Task Allows_public_addresses(string host)
    {
        (await HostBlocklist.IsBlockedAsync(host, [])).Should().BeFalse();
    }

    [Fact]
    public async Task Blocks_localhost_hostname()
    {
        (await HostBlocklist.IsBlockedAsync("localhost", [])).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"127.0.0.1\SQLEXPRESS")]
    [InlineData("tcp:127.0.0.1\\SQLEXPRESS")]
    [InlineData("tcp:127.0.0.1,1433")]
    public async Task Blocks_private_sql_server_named_instances(string host)
    {
        (await HostBlocklist.IsBlockedAsync(host, [])).Should().BeTrue();
    }

    [Fact]
    public async Task Blocks_multi_host_list_when_any_part_is_private()
    {
        (await HostBlocklist.IsBlockedAsync("8.8.8.8,127.0.0.1", [])).Should().BeTrue();
    }

    [Fact]
    public async Task Allows_multi_host_list_when_all_parts_are_public()
    {
        (await HostBlocklist.IsBlockedAsync("8.8.8.8,1.1.1.1", [])).Should().BeFalse();
    }

    [Fact]
    public async Task Blocked_list_cannot_be_bypassed_by_a_multi_host_string()
    {
        (await HostBlocklist.IsBlockedAsync(
            "169.254.169.254,8.8.8.8",
            ["169.254.169.254"],
            allowPrivateHosts: true)).Should().BeTrue();
    }

    [Fact]
    public async Task Allows_multi_host_list_when_every_part_is_allowlisted()
    {
        (await HostBlocklist.IsBlockedAsync(
            "10.0.0.5,10.0.0.6",
            [],
            allowPrivateHosts: false,
            allowedHosts: ["10.0.0.5", "10.0.0.6"])).Should().BeFalse();
    }

    [Fact]
    public async Task Allows_private_hosts_when_explicitly_enabled()
    {
        (await HostBlocklist.IsBlockedAsync(
            "127.0.0.1",
            [],
            allowPrivateHosts: true)).Should().BeFalse();
    }

    [Fact]
    public async Task Allows_explicitly_allowlisted_private_host()
    {
        (await HostBlocklist.IsBlockedAsync(
            "10.0.0.5",
            [],
            allowPrivateHosts: false,
            allowedHosts: ["10.0.0.5"])).Should().BeFalse();
    }

    [Fact]
    public async Task Blocked_list_wins_over_allowlist()
    {
        (await HostBlocklist.IsBlockedAsync(
            "metadata.google.internal",
            ["metadata.google.internal"],
            allowPrivateHosts: true,
            allowedHosts: ["metadata.google.internal"])).Should().BeTrue();
    }
}
