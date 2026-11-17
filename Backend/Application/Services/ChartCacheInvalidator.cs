using Application.Caching;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class ChartCacheInvalidator : IChartCacheInvalidator
{
    private readonly IApplicationDbContext _db;
    private readonly IChartResultCache _cache;
    private readonly ILogger<ChartCacheInvalidator> _logger;

    public ChartCacheInvalidator(
        IApplicationDbContext db,
        IChartResultCache cache,
        ILogger<ChartCacheInvalidator> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateChartsAsync(
        IReadOnlyCollection<ChartCacheTarget> targets,
        CancellationToken ct = default)
    {
        foreach (var target in targets.Distinct())
        {
            try
            {
                await _cache.InvalidateAsync(target.UserId, target.ChartId, ct);
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
                    target.UserId,
                    target.ChartId);
            }
        }
    }

    public async Task InvalidateForConnectionAsync(Guid connectionId, CancellationToken ct = default)
    {
        var charts = await _db.SavedCharts
            .AsNoTracking()
            .Where(sc => sc.ConnectionId == connectionId)
            .Select(sc => new { sc.UserId, sc.Id })
            .ToListAsync(ct);

        await InvalidateManyAsync(charts.Select(sc => (sc.UserId, sc.Id)), ct);
    }

    public async Task InvalidateForDatasetsAsync(
        IReadOnlyCollection<Guid> datasetIds,
        CancellationToken ct = default)
    {
        if (datasetIds.Count == 0)
            return;

        var ids = datasetIds.ToArray();
        var charts = await _db.SavedCharts
            .AsNoTracking()
            .Where(sc => sc.DatasetId != null && ids.Contains(sc.DatasetId.Value))
            .Select(sc => new { sc.UserId, sc.Id })
            .ToListAsync(ct);

        await InvalidateManyAsync(charts.Select(sc => (sc.UserId, sc.Id)), ct);
    }

    public Task InvalidateForUserAsync(Guid userId, CancellationToken ct = default)
        => _cache.InvalidateUserAsync(userId, ct);

    private async Task InvalidateManyAsync(
        IEnumerable<(Guid UserId, Guid ChartId)> charts,
        CancellationToken ct)
    {
        foreach (var (userId, chartId) in charts)
        {
            try
            {
                await _cache.InvalidateAsync(userId, chartId, ct);
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
    }
}
