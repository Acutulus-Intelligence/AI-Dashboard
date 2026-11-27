using Application.DTos.Response;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Application.Services;

/// <summary>
/// Loads schema metadata for a table or uploaded dataset and delegates to the
/// deterministic <see cref="ChartRecommender"/>. Results are cached per user
/// and source for a minute so entering the wizard does not repeat the schema
/// query immediately after the preview loaded it.
/// </summary>
public class ChartRecommendationService : IChartRecommendationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

    private readonly IApplicationDbContext _db;
    private readonly ISchemaInspector _schemaInspector;
    private readonly ICollectionAccessService _collectionAccess;
    private readonly IConnectionAccessService _connectionAccess;

    private sealed record CacheEntry(DateTimeOffset ExpiresAt, ChartRecommendationResponse Response);

    public ChartRecommendationService(
        IApplicationDbContext db,
        ISchemaInspector schemaInspector,
        ICollectionAccessService collectionAccess,
        IConnectionAccessService connectionAccess)
    {
        _db = db;
        _schemaInspector = schemaInspector;
        _collectionAccess = collectionAccess;
        _connectionAccess = connectionAccess;
    }

    public async Task<ChartRecommendationResponse> RecommendForTableAsync(
        Guid connectionId,
        string tableName,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!await _connectionAccess.CanViewAsync(connectionId, userId, ct))
            throw new UnauthorizedAccessException("Connection is not accessible to you.");

        var cacheKey = $"table:{userId:N}:{connectionId:N}:{tableName.Trim().ToLowerInvariant()}";
        var cached = TryGet(cacheKey);
        if (cached is not null) return cached;

        var schema = await _schemaInspector.GetTableSchemaAsync(connectionId, userId, tableName, ct);

        var response = ChartRecommender.Recommend(
            "table",
            schema.Columns.Select(c => (c.ColumnName, c.DataType, c.IsNullable)));

        Store(cacheKey, response);
        return response;
    }

    public async Task<ChartRecommendationResponse> RecommendForDatasetAsync(
        Guid collectionId,
        Guid fileId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!await _collectionAccess.CanViewAsync(collectionId, userId, ct))
            throw new UnauthorizedAccessException("Collection is not accessible to you.");

        var cacheKey = $"dataset:{userId:N}:{collectionId:N}:{fileId:N}";
        var cached = TryGet(cacheKey);
        if (cached is not null) return cached;

        var dataset = await _db.SavedDatasets
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == fileId && d.CollectionId == collectionId, ct)
            ?? throw new KeyNotFoundException("Dataset not found.");

        var columns = dataset.ColumnNames.Select((name, i) => (
            name,
            i < dataset.ColumnTypes.Length ? dataset.ColumnTypes[i] : "unknown",
            false));

        var response = ChartRecommender.Recommend("dataset", columns);
        Store(cacheKey, response);
        return response;
    }

    private static ChartRecommendationResponse? TryGet(string key)
    {
        if (!Cache.TryGetValue(key, out var entry)) return null;
        if (entry.ExpiresAt > DateTimeOffset.UtcNow) return entry.Response;

        Cache.TryRemove(key, out _);
        return null;
    }

    private static void Store(string key, ChartRecommendationResponse response)
    {
        Cache[key] = new CacheEntry(DateTimeOffset.UtcNow.Add(CacheTtl), response);

        if (Cache.Count <= 512) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var (entryKey, entry) in Cache)
        {
            if (entry.ExpiresAt <= now) Cache.TryRemove(entryKey, out _);
        }
    }
}
