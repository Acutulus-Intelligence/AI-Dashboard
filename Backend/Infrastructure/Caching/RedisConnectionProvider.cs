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
            var options = CreateOptions(_settings.RedisConnectionString);

            var connection = ConnectionMultiplexer.Connect(options);
            _logger.LogInformation("Connected to Redis for chart cache.");
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not connect to Redis for chart cache; using in-process cache only.");
            return null;
        }
    }

    private static ConfigurationOptions CreateOptions(string connectionString)
    {
        ConfigurationOptions options;

        if (connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connectionString);
            options = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
            };

            // Uri returns -1 for schemes without a registered default port.
            options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

            var database = uri.AbsolutePath.Trim('/');
            if (int.TryParse(database, out var databaseIndex) && databaseIndex >= 0)
                options.DefaultDatabase = databaseIndex;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var separator = uri.UserInfo.IndexOf(':');
                var user = separator >= 0 ? uri.UserInfo[..separator] : uri.UserInfo;
                var password = separator >= 0 ? uri.UserInfo[(separator + 1)..] : string.Empty;

                if (!string.IsNullOrEmpty(user))
                    options.User = Uri.UnescapeDataString(user);
                if (!string.IsNullOrEmpty(password))
                    options.Password = Uri.UnescapeDataString(password);
            }
        }
        else
        {
            options = ConfigurationOptions.Parse(connectionString);
        }

        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;

        return options;
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value?.Dispose();
    }
}
