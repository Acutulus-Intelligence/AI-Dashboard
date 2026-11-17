using Application.Caching;

namespace Application.Interfaces;

public interface IChartCacheInvalidator
{
    Task InvalidateChartsAsync(IReadOnlyCollection<ChartCacheTarget> targets, CancellationToken ct = default);
    Task InvalidateForConnectionAsync(Guid connectionId, CancellationToken ct = default);
    Task InvalidateForDatasetsAsync(IReadOnlyCollection<Guid> datasetIds, CancellationToken ct = default);
    Task InvalidateForUserAsync(Guid userId, CancellationToken ct = default);
}
