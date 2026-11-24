using Application.DTos.Response;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Loads schema metadata for a table or uploaded dataset and delegates to the
/// deterministic <see cref="ChartRecommender"/>.
/// </summary>
public class ChartRecommendationService : IChartRecommendationService
{
    private readonly IApplicationDbContext _db;
    private readonly ISchemaInspector _schemaInspector;
    private readonly ICollectionAccessService _collectionAccess;

    public ChartRecommendationService(
        IApplicationDbContext db,
        ISchemaInspector schemaInspector,
        ICollectionAccessService collectionAccess)
    {
        _db = db;
        _schemaInspector = schemaInspector;
        _collectionAccess = collectionAccess;
    }

    public async Task<ChartRecommendationResponse> RecommendForTableAsync(
        Guid connectionId,
        string tableName,
        Guid userId,
        CancellationToken ct = default)
    {
        var schema = await _schemaInspector.GetTableSchemaAsync(connectionId, userId, tableName, ct);

        return ChartRecommender.Recommend(
            "table",
            schema.Columns.Select(c => (c.ColumnName, c.DataType, c.IsNullable)));
    }

    public async Task<ChartRecommendationResponse> RecommendForDatasetAsync(
        Guid collectionId,
        Guid fileId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!await _collectionAccess.CanViewAsync(collectionId, userId, ct))
            throw new UnauthorizedAccessException("Collection is not accessible to you.");

        var dataset = await _db.SavedDatasets
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == fileId && d.CollectionId == collectionId, ct)
            ?? throw new KeyNotFoundException("Dataset not found.");

        var columns = dataset.ColumnNames.Select((name, i) => (
            name,
            i < dataset.ColumnTypes.Length ? dataset.ColumnTypes[i] : "unknown",
            false));

        return ChartRecommender.Recommend("dataset", columns);
    }
}
