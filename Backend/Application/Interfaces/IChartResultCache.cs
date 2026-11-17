using Application.Caching;

namespace Application.Interfaces;

public interface IChartResultCache
{
    Task<CachedChartResult?> GetAsync(Guid userId, Guid chartId, CancellationToken ct = default);
    Task SetAsync(Guid userId, Guid chartId, CachedChartResult result, CancellationToken ct = default);
    Task InvalidateAsync(Guid userId, Guid chartId, CancellationToken ct = default);
    Task InvalidateUserAsync(Guid userId, CancellationToken ct = default);
}
