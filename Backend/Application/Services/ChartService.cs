using Application.Caching;
using Application.Common.Exceptions;
using Application.Datasets;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Application.Settings;
using Domain.Charts;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class ChartService : IChartService
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queryExecutor;
    private readonly IDatasetQueryExecutor _datasetQueryExecutor;
    private readonly IDataQueryExecutor _dataQueryExecutor;
    private readonly IConnectionAccessService _access;
    private readonly ICollectionAccessService _collectionAccess;
    private readonly IChartResultCache _chartCache;
    private readonly IKeyedAsyncLock _executionLock;
    private readonly ChartCacheSettings _cacheSettings;
    private readonly ILogger<ChartService> _logger;

    public ChartService(
        IApplicationDbContext db,
        IQueryExecutor queryExecutor,
        IDatasetQueryExecutor datasetQueryExecutor,
        IDataQueryExecutor dataQueryExecutor,
        IConnectionAccessService access,
        ICollectionAccessService collectionAccess,
        IChartResultCache chartCache,
        IKeyedAsyncLock executionLock,
        IOptions<ChartCacheSettings> cacheSettings,
        ILogger<ChartService> logger)
    {
        _db = db;
        _queryExecutor = queryExecutor;
        _datasetQueryExecutor = datasetQueryExecutor;
        _dataQueryExecutor = dataQueryExecutor;
        _access = access;
        _collectionAccess = collectionAccess;
        _chartCache = chartCache;
        _executionLock = executionLock;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
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
            var dataset = await _db.SavedDatasets
                .AsNoTracking()
                .FirstOrDefaultAsync(ds => ds.Id == request.DatasetId.Value, ct);

            if (dataset is null)
                throw new KeyNotFoundException("Dataset not found.");

            var canViewCollection = await _collectionAccess.CanViewAsync(dataset.CollectionId, userId, ct);
            if (!canViewCollection)
                throw new UnauthorizedAccessException("Dataset collection is not accessible to you.");
        }

        await EnsureFolderOwnershipAsync(request.FolderId, userId, ct);

        await EnsureUniqueTitleAsync(userId, request.Title, excludeId: null, ct);

        var chart = new SavedChart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title.Trim(),
            ChartType = ChartCatalog.CanonicalId(request.ChartType) ?? request.ChartType,
            XAxis = request.XAxis,
            YAxis = request.YAxis.ToArray(),
            Aggregation = request.Aggregation,
            GroupBy = request.GroupBy,
            SqlQuery = request.SqlQuery,
            ConnectionId = request.ConnectionId,
            DatasetId = request.DatasetId,
            FolderId = request.FolderId,
            TableName = request.TableName,
            DataModel = request.DataModel,
            StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, request.ChartType),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.SavedCharts.Add(chart);
        await _db.SaveChangesAsync(ct);

        return new ChartResponse(chart.Id, chart.Title, chart.ChartType, chart.CreatedAt, chart.FolderId);
    }

    public async Task<List<ChartResponse>> GetChartsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.SavedCharts
            .AsNoTracking()
            .Where(sc => sc.UserId == userId)
            .OrderByDescending(sc => sc.CreatedAt)
            .Select(sc => new ChartResponse(sc.Id, sc.Title, sc.ChartType, sc.CreatedAt, sc.FolderId))
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
            chart.StyleConfig, FolderId: chart.FolderId
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
        chart.ChartType = ChartCatalog.CanonicalId(request.ChartType) ?? request.ChartType;
        chart.XAxis = request.XAxis;
        chart.YAxis = request.YAxis.ToArray();
        chart.Aggregation = request.Aggregation;
        chart.GroupBy = request.GroupBy;
        chart.SqlQuery = request.SqlQuery;
        chart.StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, request.ChartType);
        chart.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _chartCache.InvalidateAsync(userId, id, ct);

        return new ChartDetailResponse(
            chart.Id, chart.Title, chart.ChartType,
            chart.XAxis, [.. chart.YAxis], chart.Aggregation,
            chart.GroupBy, chart.SqlQuery,
            chart.ConnectionId, chart.DatasetId, chart.TableName, chart.CreatedAt,
            chart.StyleConfig, FolderId: chart.FolderId
        );
    }

    public ChartCatalogResponse GetCatalog() => new(ChartCatalog.Types, ChartCatalog.Palettes);

    public async Task DeleteChartAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        var referencingWidgets = await _db.DashboardWidgets
            .Where(w => w.SavedChartId == id)
            .ToListAsync(ct);

        if (referencingWidgets.Count > 0)
        {
            var dashboardIds = referencingWidgets
                .Select(w => w.DashboardId)
                .Distinct()
                .ToList();
            var affectedDashboards = await _db.Dashboards
                .Where(d => dashboardIds.Contains(d.Id))
                .ToListAsync(ct);
            var now = DateTime.UtcNow;

            foreach (var dashboard in affectedDashboards)
                dashboard.UpdatedAt = now;

            _db.DashboardWidgets.RemoveRange(referencingWidgets);
        }

        _db.SavedCharts.Remove(chart);
        await _db.SaveChangesAsync(ct);
        await _chartCache.InvalidateAsync(userId, id, ct);
    }

    public async Task MoveChartAsync(Guid id, Guid userId, Guid? folderId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        await EnsureFolderOwnershipAsync(folderId, userId, ct);

        chart.FolderId = folderId;
        chart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChartConfigResponse> ExecuteChartAsync(
        Guid id,
        Guid userId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        if (chart.ConnectionId is null && chart.DatasetId is null)
            throw new InvalidOperationException("Chart has no associated data source.");

        SavedDataset? dataset = null;

        if (chart.DatasetId is not null)
        {
            dataset = await _db.SavedDatasets
                .AsNoTracking()
                .FirstOrDefaultAsync(ds => ds.Id == chart.DatasetId.Value, ct)
                ?? throw new KeyNotFoundException("Dataset not found.");

            if (!await _collectionAccess.CanViewAsync(dataset.CollectionId, userId, ct))
                throw new UnauthorizedAccessException("Dataset collection is not accessible to you.");
        }
        else if (!await _access.CanViewAsync(chart.ConnectionId!.Value, userId, ct))
        {
            throw new UnauthorizedAccessException("Connection is not accessible to you.");
        }

        if (!forceRefresh)
        {
            var cached = await TryGetCachedAsync(chart, userId, ct);
            if (cached is not null)
                return BuildResponse(chart, cached.Rows, cached.DataUpdatedAt);
        }

        // Collapse concurrent cache misses for the same chart into one query.
        using var lease = await _executionLock.AcquireAsync($"{userId:N}:{id:N}", ct);

        if (!forceRefresh)
        {
            var cached = await TryGetCachedAsync(chart, userId, ct);
            if (cached is not null)
                return BuildResponse(chart, cached.Rows, cached.DataUpdatedAt);
        }

        List<Dictionary<string, object?>> result;

        if (dataset is not null)
        {
            var columnNames = dataset.ColumnNames;
            var columnTypes = dataset.ColumnTypes;
            var rows = DatasetRows.Decode(dataset);

            if (chart.DataModel is not null)
            {
                result = await _dataQueryExecutor.ExecuteAsync(
                    columnNames, columnTypes, rows, chart.DataModel, ct);
            }
            else
            {
                result = await _datasetQueryExecutor.ExecuteAsync(
                    columnNames, columnTypes, rows, dataset.TableName, chart.SqlQuery, ct);
            }
        }
        else
        {
            result = await _queryExecutor.ExecuteAsync(
                chart.ConnectionId!.Value, userId, chart.SqlQuery, ct);
        }

        var dataUpdatedAt = DateTimeOffset.UtcNow;
        var cacheTtl = chart.ConnectionId is not null && _cacheSettings.ConnectionEntryTtlMinutes > 0
            ? TimeSpan.FromMinutes(_cacheSettings.ConnectionEntryTtlMinutes)
            : (TimeSpan?)null;

        try
        {
            await _chartCache.SetAsync(
                userId,
                id,
                new CachedChartResult(userId, id, result, chart.UpdatedAt, dataUpdatedAt),
                cacheTtl,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to cache chart result for user {UserId} and chart {ChartId}.",
                userId,
                id);
        }

        return BuildResponse(chart, result, dataUpdatedAt);
    }

    private async Task<CachedChartResult?> TryGetCachedAsync(
        SavedChart chart,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var cached = await _chartCache.GetAsync(userId, chart.Id, ct);
            if (cached is null)
                return null;

            if (cached.ChartUpdatedAt != chart.UpdatedAt)
            {
                await _chartCache.InvalidateAsync(userId, chart.Id, ct);
                return null;
            }

            return cached;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read chart cache for user {UserId} and chart {ChartId}.",
                userId,
                chart.Id);
            return null;
        }
    }

    private static ChartConfigResponse BuildResponse(
        SavedChart chart,
        List<Dictionary<string, object?>> rows,
        DateTimeOffset dataUpdatedAt)
    {
        return new ChartConfigResponse(
            chart.ChartType,
            chart.Title,
            chart.XAxis,
            [.. chart.YAxis],
            chart.Aggregation,
            chart.GroupBy,
            chart.SqlQuery,
            rows,
            chart.StyleConfig,
            DataUpdatedAt: dataUpdatedAt
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

    private async Task EnsureFolderOwnershipAsync(
        Guid? folderId, Guid userId, CancellationToken ct)
    {
        if (folderId is null)
            return;

        var exists = await _db.ChartFolders.AnyAsync(
            f => f.Id == folderId.Value && f.UserId == userId, ct);

        if (!exists)
            throw new KeyNotFoundException("Folder not found.");
    }
}
