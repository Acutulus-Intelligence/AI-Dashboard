using Application.Common.Exceptions;
using Application.Datasets;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Charts;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ChartService : IChartService
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queryExecutor;
    private readonly IDatasetQueryExecutor _datasetQueryExecutor;
    private readonly IConnectionAccessService _access;

    public ChartService(
        IApplicationDbContext db,
        IQueryExecutor queryExecutor,
        IDatasetQueryExecutor datasetQueryExecutor,
        IConnectionAccessService access)
    {
        _db = db;
        _queryExecutor = queryExecutor;
        _datasetQueryExecutor = datasetQueryExecutor;
        _access = access;
    }

    public async Task<ChartResponse> SaveChartAsync(Guid userId, SaveChartRequest request, CancellationToken ct = default)
    {
        if (request.ConnectionId.HasValue && request.DatasetId.HasValue)
            throw new InvalidOperationException("A chart can reference either a connection or a dataset, not both.");

        if (request.ConnectionId.HasValue)
        {
            var canViewConnection = await _access.CanViewAsync(request.ConnectionId.Value, userId, ct);

            if (!canViewConnection)
                throw new UnauthorizedAccessException("Connection is not accessible to you.");
        }

        if (request.DatasetId.HasValue)
        {
            var datasetExists = await _db.SavedDatasets
                .AnyAsync(ds => ds.Id == request.DatasetId.Value && ds.UserId == userId, ct);

            if (!datasetExists)
                throw new KeyNotFoundException("Dataset not found.");
        }

        await EnsureUniqueTitleAsync(userId, request.Title, excludeId: null, ct);

        var chart = new SavedChart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title.Trim(),
            ChartType = request.ChartType,
            XAxis = request.XAxis,
            YAxis = request.YAxis.ToArray(),
            Aggregation = request.Aggregation,
            GroupBy = request.GroupBy,
            SqlQuery = request.SqlQuery,
            ConnectionId = request.ConnectionId,
            DatasetId = request.DatasetId,
            TableName = request.TableName,
            StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, request.ChartType),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.SavedCharts.Add(chart);
        await _db.SaveChangesAsync(ct);

        return new ChartResponse(chart.Id, chart.Title, chart.ChartType, chart.CreatedAt);
    }

    public async Task<List<ChartResponse>> GetChartsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.SavedCharts
            .AsNoTracking()
            .Where(sc => sc.UserId == userId)
            .OrderByDescending(sc => sc.CreatedAt)
            .Select(sc => new ChartResponse(sc.Id, sc.Title, sc.ChartType, sc.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ChartDetailResponse> GetChartAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        return new ChartDetailResponse(
            chart.Id, chart.Title, chart.ChartType,
            chart.XAxis, [.. chart.YAxis], chart.Aggregation,
            chart.GroupBy, chart.SqlQuery,
            chart.ConnectionId, chart.DatasetId, chart.TableName, chart.CreatedAt,
            chart.StyleConfig
        );
    }

    public async Task<ChartDetailResponse> UpdateChartAsync(
        Guid id, Guid userId, UpdateChartRequest request, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        await EnsureUniqueTitleAsync(userId, request.Title, excludeId: id, ct);

        chart.Title = request.Title.Trim();
        chart.ChartType = request.ChartType;
        chart.StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, request.ChartType);
        chart.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ChartDetailResponse(
            chart.Id, chart.Title, chart.ChartType,
            chart.XAxis, [.. chart.YAxis], chart.Aggregation,
            chart.GroupBy, chart.SqlQuery,
            chart.ConnectionId, chart.DatasetId, chart.TableName, chart.CreatedAt,
            chart.StyleConfig
        );
    }

    public ChartCatalogResponse GetCatalog() => new(ChartCatalog.Types, ChartCatalog.Palettes);

    public async Task DeleteChartAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        _db.SavedCharts.Remove(chart);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChartConfigResponse> ExecuteChartAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        if (chart.ConnectionId is null && chart.DatasetId is null)
            throw new InvalidOperationException("Chart has no associated data source.");

        List<Dictionary<string, object?>> result;

        if (chart.DatasetId is not null)
        {
            var dataset = await _db.SavedDatasets
                .AsNoTracking()
                .FirstOrDefaultAsync(ds => ds.Id == chart.DatasetId.Value && ds.UserId == userId, ct)
                ?? throw new KeyNotFoundException("Dataset not found.");

            result = await _datasetQueryExecutor.ExecuteAsync(
                dataset.ColumnNames,
                dataset.ColumnTypes,
                DatasetRows.Decode(dataset),
                dataset.TableName,
                chart.SqlQuery,
                ct);
        }
        else
        {
            result = await _queryExecutor.ExecuteAsync(chart.ConnectionId!.Value, userId, chart.SqlQuery, ct);
        }

        return new ChartConfigResponse(
            chart.ChartType,
            chart.Title,
            chart.XAxis,
            [.. chart.YAxis],
            chart.Aggregation,
            chart.GroupBy,
            chart.SqlQuery,
            result,
            chart.StyleConfig
        );
    }

    private async Task EnsureUniqueTitleAsync(
        Guid userId, string title, Guid? excludeId, CancellationToken ct)
    {
        var trimmed = title.Trim();
        var taken = await _db.SavedCharts.AnyAsync(
            sc => sc.UserId == userId
                && sc.Title == trimmed
                && (!excludeId.HasValue || sc.Id != excludeId.Value),
            ct);

        if (taken)
        {
            throw new ConflictException(
                $"A chart named \"{trimmed}\" already exists. Rename it and try again.",
                "chart_title_conflict");
        }
    }
}
