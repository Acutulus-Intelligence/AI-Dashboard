using StackExchange.Redis;

namespace Infrastructure.Caching;

public interface IRedisConnectionProvider
{
    IConnectionMultiplexer? Connection { get; }
}
