using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Application.Caching;
using Application.Interfaces;
using Application.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisChartResultCache : IChartResultCache, IChartCacheInvalidationHandler
{
    private const int MaxInvalidationMessageBytes = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string GetInvalidationChannel(string instanceName)
        => $"{instanceName}chart-cache:invalidate";

    private readonly IMemoryCache _memory;
    private readonly IRedisConnectionProvider _redisProvider;
    private readonly IEncryptionService _encryption;
    private readonly ChartCacheSettings _settings;
    private readonly ILogger<RedisChartResultCache> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, byte> _l1Keys = new();

    public RedisChartResultCache(
        [FromKeyedServices(ChartCacheSettings.L1CacheKey)] IMemoryCache memory,
        IRedisConnectionProvider redisProvider,
        IEncryptionService encryption,
        IOptions<ChartCacheSettings> settings,
        ILogger<RedisChartResultCache> logger)
    {
        _memory = memory;
        _redisProvider = redisProvider;
        _encryption = encryption;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CachedChartResult?> GetAsync(
        Guid userId,
        Guid chartId,
        CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return null;

        var key = BuildKey(userId, chartId);

        if (_memory.TryGetValue(key, out CachedChartResult? local) && local is not null)
            return local;

        var database = GetDatabase();
        if (database is null)
            return null;

        try
        {
            var value = await database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            var payload = value.ToString();
            if (_settings.EncryptPayloads)
                payload = _encryption.Decrypt(payload);

            var result = JsonSerializer.Deserialize<CachedChartResult>(payload, JsonOptions);
            if (result is null)
                return null;

            SetLocal(key, result, Encoding.UTF8.GetByteCount(payload));
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read chart cache for user {UserId} and chart {ChartId}.",
                userId,
                chartId);
            return null;
        }
    }

    public async Task SetAsync(
        Guid userId,
        Guid chartId,
        CachedChartResult result,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var key = BuildKey(userId, chartId);
        string payload;

        try
        {
            payload = JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to serialize chart cache result for user {UserId} and chart {ChartId}.",
                userId,
                chartId);
            return;
        }

        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        if (_settings.MaxPayloadBytes > 0 && payloadBytes > _settings.MaxPayloadBytes)
        {
            _logger.LogDebug(
                "Skipping chart cache write for user {UserId} and chart {ChartId} because the payload is {Bytes} bytes.",
                userId,
                chartId,
                payloadBytes);
            return;
        }

        SetLocal(key, result, payloadBytes);

        var database = GetDatabase();
        if (database is null)
            return;

        try
        {
            var stored = _settings.EncryptPayloads ? _encryption.Encrypt(payload) : payload;
            var lifetime = ttl ?? (_settings.EntryTtlDays > 0
                ? TimeSpan.FromDays(_settings.EntryTtlDays)
                : (TimeSpan?)null);
            var expiry = lifetime.HasValue ? new Expiration(lifetime.Value) : Expiration.Default;

            await database.StringSetAsync(key, stored, expiry);
            await PublishAsync(new ChartCacheInvalidationMessage(
                _instanceId, "updated", userId, chartId));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write chart cache for user {UserId} and chart {ChartId}.",
                userId,
                chartId);
        }
    }

    public async Task InvalidateAsync(
        Guid userId,
        Guid chartId,
        CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var key = BuildKey(userId, chartId);
        RemoveLocal(key);

        var database = GetDatabase();
        if (database is null)
            return;

        try
        {
            await database.KeyDeleteAsync(key);
            await PublishAsync(new ChartCacheInvalidationMessage(
                _instanceId, "invalidated", userId, chartId));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to invalidate chart cache for user {UserId} and chart {ChartId}.",
                userId,
                chartId);
        }
    }

    public async Task InvalidateUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var prefix = BuildUserPrefix(userId);
        RemoveLocalByPrefix(prefix);

        var connection = _redisProvider.Connection;
        var database = connection?.GetDatabase();
        if (connection is null || database is null)
            return;

        try
        {
            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: prefix + "*", pageSize: 500))
                    await database.KeyDeleteAsync(key);
            }

            await PublishAsync(new ChartCacheInvalidationMessage(
                _instanceId, "user", userId, null));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to purge chart cache for user {UserId}.",
                userId);
        }
    }

    public void Handle(string message)
    {
        if (message.Length > MaxInvalidationMessageBytes)
        {
            _logger.LogWarning("Ignored oversized chart cache invalidation message.");
            return;
        }

        try
        {
            var invalidation = JsonSerializer.Deserialize<ChartCacheInvalidationMessage>(message, JsonOptions);
            if (invalidation is null || invalidation.InstanceId == _instanceId)
                return;

            if (invalidation.Kind == "user" && invalidation.UserId.HasValue)
            {
                RemoveLocalByPrefix(BuildUserPrefix(invalidation.UserId.Value));
                return;
            }

            if (invalidation.UserId.HasValue && invalidation.ChartId.HasValue)
                RemoveLocal(BuildKey(invalidation.UserId.Value, invalidation.ChartId.Value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process chart cache invalidation message.");
        }
    }

    private string BuildKey(Guid userId, Guid chartId)
        => $"{_settings.InstanceName}chart-result:v1:{userId:N}:{chartId:N}";

    private string BuildUserPrefix(Guid userId)
        => $"{_settings.InstanceName}chart-result:v1:{userId:N}:";

    private IDatabase? GetDatabase() => _redisProvider.Connection?.GetDatabase();

    private void SetLocal(string key, CachedChartResult result, long size)
    {
        if (_settings.L1SizeLimitBytes > 0 && size > _settings.L1SizeLimitBytes)
            return;

        var options = new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, size),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(
                Math.Max(1, _settings.L1TtlSeconds)),
        };
        options.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
        {
            if (evictedKey is string key && state is ConcurrentDictionary<string, byte> keys)
                keys.TryRemove(key, out _);
        }, _l1Keys);

        _memory.Set(key, result, options);
        _l1Keys[key] = 0;
    }

    private void RemoveLocal(string key)
    {
        _memory.Remove(key);
        _l1Keys.TryRemove(key, out _);
    }

    private void RemoveLocalByPrefix(string prefix)
    {
        foreach (var key in _l1Keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                RemoveLocal(key);
        }
    }

    private async Task PublishAsync(ChartCacheInvalidationMessage message)
    {
        var connection = _redisProvider.Connection;
        if (connection is null)
            return;

        try
        {
            await connection.GetSubscriber().PublishAsync(
                RedisChannel.Literal(GetInvalidationChannel(_settings.InstanceName)),
                JsonSerializer.Serialize(message, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish chart cache invalidation.");
        }
    }
}
