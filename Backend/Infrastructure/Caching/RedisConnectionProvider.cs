using Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisConnectionProvider : IRedisConnectionProvider, IDisposable
{
    private readonly ChartCacheSettings _settings;
    private readonly ILogger<RedisConnectionProvider> _logger;
    private readonly Lazy<IConnectionMultiplexer?> _connection;

    public RedisConnectionProvider(
        IOptions<ChartCacheSettings> settings,
        ILogger<RedisConnectionProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _connection = new Lazy<IConnectionMultiplexer?>(CreateConnection, isThreadSafe: true);
    }

    public IConnectionMultiplexer? Connection =>
        _settings.Enabled && _settings.RedisEnabled ? _connection.Value : null;

    private IConnectionMultiplexer? CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_settings.RedisConnectionString))
        {
            _logger.LogWarning(
                "Chart cache Redis is not configured; using in-process cache only.");
            return null;
        }

        try
        {
            var options = ConfigurationOptions.Parse(_settings.RedisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;

            var connection = ConnectionMultiplexer.Connect(options);
            _logger.LogInformation("Connected to Redis for chart cache.");
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not connect to Redis for chart cache; using in-process cache only.");
            return null;
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value?.Dispose();
    }
}
