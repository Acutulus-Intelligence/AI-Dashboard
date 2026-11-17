using System.Text.Json;
using Application.Caching;
using Application.Settings;
using FluentAssertions;
using Infrastructure.Caching;
using Infrastructure.Encryption;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.Redis;

namespace Presentation.IntegrationTests;

public sealed class ChartRedisCacheTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync() => await _redis.StartAsync();

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    [Fact]
    public async Task Redis_cache_round_trips_encrypted_rows_and_invalidates()
    {
        var settings = new ChartCacheSettings
        {
            Enabled = true,
            RedisEnabled = true,
            RedisConnectionString = _redis.GetConnectionString(),
            InstanceName = $"test-{Guid.NewGuid():N}:",
            L1TtlSeconds = 60,
            EntryTtlDays = 1,
            EncryptPayloads = true,
        };
        var options = Options.Create(settings);
        var encryption = new AesEncryptionService(Options.Create(new EncryptionSettings
        {
            Key = "chart-cache-test-encryption-key-32b!",
        }));

        using var firstProvider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var secondProvider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var thirdProvider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var firstL1 = new MemoryCache(new MemoryCacheOptions());
        using var secondL1 = new MemoryCache(new MemoryCacheOptions());
        using var thirdL1 = new MemoryCache(new MemoryCacheOptions());

        var first = new RedisChartResultCache(
            firstL1, firstProvider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);
        var second = new RedisChartResultCache(
            secondL1, secondProvider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);
        var third = new RedisChartResultCache(
            thirdL1, thirdProvider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);

        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var dataUpdatedAt = DateTimeOffset.UtcNow;
        var entry = new CachedChartResult(
            [new Dictionary<string, object?> { ["amount"] = 42 }],
            DateTime.UtcNow,
            dataUpdatedAt);

        await first.SetAsync(userId, chartId, entry);

        // Second instance has no L1 entry, so this exercises Redis decryption
        // and JSON deserialization rather than the in-process cache.
        var cached = await second.GetAsync(userId, chartId);

        cached.Should().NotBeNull();
        cached!.ChartUpdatedAt.Should().Be(entry.ChartUpdatedAt);
        cached.DataUpdatedAt.Should().Be(dataUpdatedAt);
        cached.Rows.Should().ContainSingle();
        var amount = cached.Rows[0]["amount"].Should().BeOfType<JsonElement>().Subject;
        amount.GetInt32().Should().Be(42);

        await first.InvalidateAsync(userId, chartId);

        // A fresh L1 cannot see the deleted Redis key. (The second instance's
        // L1 is cleared in production by the pub/sub subscriber.)
        (await third.GetAsync(userId, chartId)).Should().BeNull();
    }
}
