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
            userId,
            chartId,
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

    [Fact]
    public async Task Redis_cache_honours_ttl_override()
    {
        var settings = new ChartCacheSettings
        {
            Enabled = true,
            RedisEnabled = true,
            RedisConnectionString = _redis.GetConnectionString(),
            InstanceName = $"test-{Guid.NewGuid():N}:",
            L1TtlSeconds = 60,
            EntryTtlDays = 30,
            EncryptPayloads = true,
        };
        var options = Options.Create(settings);
        var encryption = new AesEncryptionService(Options.Create(new EncryptionSettings
        {
            Key = "chart-cache-test-encryption-key-32b!",
        }));

        using var provider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        var cache = new RedisChartResultCache(
            l1, provider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);

        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var entry = new CachedChartResult(
            userId,
            chartId,
            [],
            DateTime.UtcNow,
            DateTimeOffset.UtcNow);

        await cache.SetAsync(userId, chartId, entry, TimeSpan.FromMinutes(2));

        var key = $"{settings.InstanceName}chart-result:v1:{userId:N}:{chartId:N}";
        var ttl = await provider.Connection!.GetDatabase().KeyTimeToLiveAsync(key);

        ttl.Should().NotBeNull();
        ttl!.Value.TotalMinutes.Should().BeInRange(1.5, 2.1);
    }

    [Fact]
    public async Task Redis_cache_accepts_redis_uri_connection_string()
    {
        var settings = new ChartCacheSettings
        {
            Enabled = true,
            RedisEnabled = true,
            RedisConnectionString = $"redis://localhost:{_redis.GetMappedPublicPort(6379)}/2",
            InstanceName = $"test-{Guid.NewGuid():N}:",
        };
        var options = Options.Create(settings);

        using var provider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);

        provider.Connection.Should().NotBeNull();
        provider.Connection!.GetDatabase().Database.Should().Be(2);
        var ping = await provider.Connection.GetDatabase().PingAsync();
        ping.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Redis_cache_discards_entries_that_do_not_match_the_key()
    {
        var settings = new ChartCacheSettings
        {
            Enabled = true,
            RedisEnabled = true,
            RedisConnectionString = _redis.GetConnectionString(),
            InstanceName = $"test-{Guid.NewGuid():N}:",
            EntryTtlDays = 1,
            EncryptPayloads = true,
        };
        var options = Options.Create(settings);
        var encryption = new AesEncryptionService(Options.Create(new EncryptionSettings
        {
            Key = "chart-cache-test-encryption-key-32b!",
        }));

        using var provider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var readerL1 = new MemoryCache(new MemoryCacheOptions());
        var reader = new RedisChartResultCache(
            readerL1, provider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);

        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var key = $"{settings.InstanceName}chart-result:v1:{userId:N}:{chartId:N}";
        var database = provider.Connection!.GetDatabase();

        var entry = new CachedChartResult(
            Guid.NewGuid(),
            chartId,
            [new Dictionary<string, object?> { ["amount"] = 99 }],
            DateTime.UtcNow,
            DateTimeOffset.UtcNow);

        // SetAsync refuses to persist a payload bound to another key.
        await reader.SetAsync(userId, chartId, entry);
        (await database.KeyExistsAsync(key)).Should().BeFalse();

        // Even a payload written directly to Redis is rejected on read and removed.
        var payload = JsonSerializer.Serialize(
            entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await database.StringSetAsync(key, encryption.Encrypt(payload), TimeSpan.FromMinutes(5));

        (await reader.GetAsync(userId, chartId)).Should().BeNull();
        (await database.KeyExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Redis_cache_removes_corrupt_entries()
    {
        var settings = new ChartCacheSettings
        {
            Enabled = true,
            RedisEnabled = true,
            RedisConnectionString = _redis.GetConnectionString(),
            InstanceName = $"test-{Guid.NewGuid():N}:",
            EntryTtlDays = 1,
            EncryptPayloads = true,
        };
        var options = Options.Create(settings);
        var encryption = new AesEncryptionService(Options.Create(new EncryptionSettings
        {
            Key = "chart-cache-test-encryption-key-32b!",
        }));

        using var provider = new RedisConnectionProvider(
            options, NullLogger<RedisConnectionProvider>.Instance);
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        var cache = new RedisChartResultCache(
            l1, provider, encryption, options,
            NullLogger<RedisChartResultCache>.Instance);

        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var key = $"{settings.InstanceName}chart-result:v1:{userId:N}:{chartId:N}";
        var database = provider.Connection!.GetDatabase();
        await database.StringSetAsync(key, "not-a-valid-payload", TimeSpan.FromMinutes(5));

        (await cache.GetAsync(userId, chartId)).Should().BeNull();
        (await database.KeyExistsAsync(key)).Should().BeFalse();
    }
}
