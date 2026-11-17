namespace Infrastructure.Caching;

public interface IChartCacheInvalidationHandler
{
    void Handle(string message);
}
