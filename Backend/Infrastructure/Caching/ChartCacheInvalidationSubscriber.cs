using Application.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class ChartCacheInvalidationSubscriber : BackgroundService
{
    private readonly IRedisConnectionProvider _redisProvider;
    private readonly IChartCacheInvalidationHandler _handler;
    private readonly ILogger<ChartCacheInvalidationSubscriber> _logger;
    private readonly string _channel;

    public ChartCacheInvalidationSubscriber(
        IRedisConnectionProvider redisProvider,
        IChartCacheInvalidationHandler handler,
        IOptions<ChartCacheSettings> settings,
        ILogger<ChartCacheInvalidationSubscriber> logger)
    {
        _redisProvider = redisProvider;
        _handler = handler;
        _logger = logger;
        _channel = RedisChartResultCache.GetInvalidationChannel(settings.Value.InstanceName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = _redisProvider.Connection;
        if (connection is null)
            return;

        try
        {
            var subscriber = connection.GetSubscriber();
            await subscriber.SubscribeAsync(
                RedisChannel.Literal(_channel),
                (_, message) =>
                {
                    if (message.HasValue)
                        _handler.Handle(message.ToString());
                });

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chart cache invalidation subscriber stopped.");
        }
    }
}
