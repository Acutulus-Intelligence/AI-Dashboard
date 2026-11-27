using Application.Caching;
using Application.Common.Exceptions;
using Application.Datasets;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Application.Settings;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class ChartService : IChartService
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queryExecutor;
    private readonly ISqlValidator _sqlValidator;
    private readonly IAuditTrail _audit;
    private readonly IDatasetQueryExecutor _datasetQueryExecutor;
    private readonly IDataQueryExecutor _dataQueryExecutor;
    private readonly IConnectionAccessService _access;
    private readonly ICollectionAccessService _collectionAccess;
    private readonly IChartResultCache _chartCache;
    private readonly IKeyedAsyncLock _executionLock;
    private readonly ChartCacheSettings _cacheSettings;
    private readonly ChartSettings _chartSettings;
    private readonly ILogger<ChartService> _logger;

    public ChartService(
        IApplicationDbContext db,
        IQueryExecutor queryExecutor,
        ISqlValidator sqlValidator,
        IAuditTrail audit,
        IDatasetQueryExecutor datasetQueryExecutor,
        IDataQueryExecutor dataQueryExecutor,
        IConnectionAccessService access,
        ICollectionAccessService collectionAccess,
        IChartResultCache chartCache,
        IKeyedAsyncLock executionLock,
        IOptions<ChartCacheSettings> cacheSettings,
        IOptions<ChartSettings> chartSettings,
        ILogger<ChartService> logger)
    {
        _db = db;
        _queryExecutor = queryExecutor;
        _sqlValidator = sqlValidator;
        _audit = audit;
        _datasetQueryExecutor = datasetQueryExecutor;
        _dataQueryExecutor = dataQueryExecutor;
        _access = access;
        _collectionAccess = collectionAccess;
        _chartCache = chartCache;
        _executionLock = executionLock;
        _cacheSettings = cacheSettings.Value;
        _chartSettings = chartSettings.Value;
        _logger = logger;
    }

    public async Task<ChartResponse> SaveChartAsync(Guid userId, SaveChartRequest request, CancellationToken ct = default)
    {
        if (request.ConnectionId.HasValue && request.DatasetId.HasValue)
            throw new InvalidOperationException("A chart can reference either a connection or a dataset, not both.");

        SavedDataset? dataset = null;

        if (request.ConnectionId.HasValue)
        {
            if (request.DataModel is not null)
                throw new ArgumentException("A data model is only supported for dataset charts.");

            if (string.IsNullOrWhiteSpace(request.SqlQuery))
                throw new ArgumentException("A chart on a database connection needs a SQL query.");

            var connection = await _db.ExternalConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(ec => ec.Id == request.ConnectionId.Value, ct)
                ?? throw new KeyNotFoundException("Connection not found.");

            var canViewConnection = await _access.CanViewAsync(request.ConnectionId.Value, userId, ct);

            if (!canViewConnection)
                throw new UnauthorizedAccessException("Connection is not accessible to you.");

            if (_chartSettings.RequireManageForSharedConnectionSql
                && connection.CompanyId is not null
                && connection.Visibility != ConnectionVisibility.Private
                && !await _access.CanManageAsync(connection.Id, userId, ct))
            {
                throw new ForbiddenException(
                    "Only users who can manage this shared connection may save charts with SQL on it.");
            }
        }

        if (request.DatasetId.HasValue)
        {
            dataset = await _db.SavedDatasets
                .AsNoTracking()
                .FirstOrDefaultAsync(ds => ds.Id == request.DatasetId.Value, ct);

            if (dataset is null)
                throw new KeyNotFoundException("Dataset not found.");

            var canViewCollection = await _collectionAccess.CanViewAsync(dataset.CollectionId, userId, ct);
            if (!canViewCollection)
                throw new UnauthorizedAccessException("Dataset collection is not accessible to you.");

            if (request.DataModel is not null)
                EnsureGroundedDataModel(request.DataModel, dataset.ColumnNames);
        }

        await EnsureFolderOwnershipAsync(request.FolderId, userId, ct);

        await EnsureUniqueTitleAsync(userId, request.Title, excludeId: null, ct);

        await EnsureSafeQueryAsync(request.ConnectionId, request.DatasetId, request.SqlQuery, ct);

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

        _audit.Record(new AuditEntry(
            AuditAction.ChartSaved,
            userId,
            chart.ConnectionId,
            chart.DatasetId,
            chart.Id,
            chart.ChartType,
            chart.SqlQuery,
            null,
            "success"));

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
            chart.StyleConfig, DataModel: chart.DataModel, FolderId: chart.FolderId,
            CollectionId: await ResolveCollectionIdAsync(chart, ct)
        );
    }

    public async Task<ChartDetailResponse> UpdateChartAsync(
        Guid id, Guid userId, UpdateChartRequest request, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        if (chart.ConnectionId.HasValue)
        {
            if (request.DataModel is not null)
                throw new ArgumentException("A data model is only supported for dataset charts.");

            if (string.IsNullOrWhiteSpace(request.SqlQuery))
                throw new ArgumentException("A chart on a database connection needs a SQL query.");
        }
        else if (chart.DatasetId.HasValue && request.DataModel is not null)
        {
            var dataset = await _db.SavedDatasets
                .AsNoTracking()
                .FirstOrDefaultAsync(ds => ds.Id == chart.DatasetId.Value, ct)
                ?? throw new KeyNotFoundException("Dataset not found.");

            EnsureGroundedDataModel(request.DataModel, dataset.ColumnNames);
        }

        if (_chartSettings.RequireManageForSharedConnectionSql && chart.ConnectionId.HasValue)
        {
            if (!await _access.CanViewAsync(chart.ConnectionId.Value, userId, ct))
                throw new UnauthorizedAccessException("Connection is not accessible to you.");

            var connection = await _db.ExternalConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(ec => ec.Id == chart.ConnectionId.Value, ct);

            if (connection is { CompanyId: not null }
                && connection.Visibility != ConnectionVisibility.Private
                && !await _access.CanManageAsync(connection.Id, userId, ct))
            {
                throw new ForbiddenException(
                    "Only users who can manage this shared connection may update charts with SQL on it.");
            }
        }

        await EnsureSafeQueryAsync(chart.ConnectionId, chart.DatasetId, request.SqlQuery, ct);

        await EnsureUniqueTitleAsync(userId, request.Title, excludeId: id, ct);

        chart.Title = request.Title.Trim();
        chart.ChartType = ChartCatalog.CanonicalId(request.ChartType) ?? request.ChartType;
        chart.XAxis = request.XAxis;
        chart.YAxis = request.YAxis.ToArray();
        chart.Aggregation = request.Aggregation;
        chart.GroupBy = request.GroupBy;
        chart.SqlQuery = request.SqlQuery;
        if (request.DataModel is not null)
            chart.DataModel = request.DataModel;
        chart.StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, request.ChartType);
        chart.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _chartCache.InvalidateAsync(userId, id, ct);

        _audit.Record(new AuditEntry(
            AuditAction.ChartUpdated,
            userId,
            chart.ConnectionId,
            chart.DatasetId,
            chart.Id,
            chart.ChartType,
            chart.SqlQuery,
            null,
            "success"));

        return new ChartDetailResponse(
            chart.Id, chart.Title, chart.ChartType,
            chart.XAxis, [.. chart.YAxis], chart.Aggregation,
            chart.GroupBy, chart.SqlQuery,
            chart.ConnectionId, chart.DatasetId, chart.TableName, chart.CreatedAt,
            chart.StyleConfig, DataModel: chart.DataModel, FolderId: chart.FolderId,
            CollectionId: await ResolveCollectionIdAsync(chart, ct)
        );
    }

    public async Task<ChartDetailResponse> UpdateChartStyleAsync(
        Guid id, Guid userId, UpdateChartStyleRequest request, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        // Presentation only: the cached query result is still valid. Bumping
        // UpdatedAt (the cache version key) or invalidating here would re-run the
        // user's SQL on every colour tweak from the quick-style popover.
        chart.StyleConfig = ChartStyleSanitizer.Sanitize(request.StyleConfig, chart.ChartType);

        await _db.SaveChangesAsync(ct);

        _audit.Record(new AuditEntry(
            AuditAction.ChartUpdated,
            userId,
            chart.ConnectionId,
            chart.DatasetId,
            chart.Id,
            chart.ChartType,
            chart.SqlQuery,
            null,
            "success"));

        return new ChartDetailResponse(
            chart.Id, chart.Title, chart.ChartType,
            chart.XAxis, [.. chart.YAxis], chart.Aggregation,
            chart.GroupBy, chart.SqlQuery,
            chart.ConnectionId, chart.DatasetId, chart.TableName, chart.CreatedAt,
            chart.StyleConfig, DataModel: chart.DataModel, FolderId: chart.FolderId,
            CollectionId: await ResolveCollectionIdAsync(chart, ct)
        );
    }

    public ChartCatalogResponse GetCatalog() => new(ChartCatalog.Types, ChartCatalog.Palettes);

    public async Task DeleteChartAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var chart = await _db.SavedCharts
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Chart not found.");

        // Widgets are removed by the database (FK cascade), but their dashboards
        // still need a fresh UpdatedAt so clients reload without the deleted widget.
        var dashboardIds = await _db.DashboardWidgets
            .Where(w => w.SavedChartId == id)
            .Select(w => w.DashboardId)
            .Distinct()
            .ToListAsync(ct);

        if (dashboardIds.Count > 0)
        {
            var affectedDashboards = await _db.Dashboards
                .Where(d => dashboardIds.Contains(d.Id))
                .ToListAsync(ct);
            var now = DateTime.UtcNow;

            foreach (var dashboard in affectedDashboards)
                dashboard.UpdatedAt = now;
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

        if (!forceRefresh && IsCacheEnabled(chart))
        {
            var cached = await TryGetCachedAsync(chart, userId, ct);
            if (cached is not null)
            {
                _audit.Record(new AuditEntry(
                    AuditAction.ChartExecuted,
                    userId,
                    chart.ConnectionId,
                    chart.DatasetId,
                    chart.Id,
                    chart.ChartType,
                    chart.SqlQuery,
                    cached.Rows.Count,
                    "success",
                    "cache"));

                return BuildResponse(chart, cached.Rows, cached.DataUpdatedAt, cached.Truncated);
            }
        }

        // Collapse concurrent cache misses for the same chart into one query.
        using var lease = await _executionLock.AcquireAsync($"{userId:N}:{id:N}", ct);

        if (!forceRefresh && IsCacheEnabled(chart))
        {
            var cached = await TryGetCachedAsync(chart, userId, ct);
            if (cached is not null)
            {
                _audit.Record(new AuditEntry(
                    AuditAction.ChartExecuted,
                    userId,
                    chart.ConnectionId,
                    chart.DatasetId,
                    chart.Id,
                    chart.ChartType,
                    chart.SqlQuery,
                    cached.Rows.Count,
                    "success",
                    "cache"));

                return BuildResponse(chart, cached.Rows, cached.DataUpdatedAt, cached.Truncated);
            }
        }

        List<Dictionary<string, object?>> result;
        var truncated = false;

        // A dashboard refresh fans out one request per chart. Bound the total
        // concurrent queries so a large dashboard cannot exhaust the external
        // database's connection pool.
        await QueryConcurrency.WaitAsync(ct);
        try
        {
            if (dataset is not null)
            {
                var columnNames = dataset.ColumnNames;
                var columnTypes = dataset.ColumnTypes;
                var rows = DatasetRows.Decode(dataset);

                if (chart.DataModel is not null)
                {
                    result = await _dataQueryExecutor.ExecuteAsync(
                        columnNames, columnTypes, rows, chart.DataModel, ct);
                    truncated = _dataQueryExecutor.LastResultTruncated;
                }
                else
                {
                    result = await _datasetQueryExecutor.ExecuteAsync(
                        columnNames, columnTypes, rows, dataset.TableName, chart.SqlQuery, ct);
                    truncated = _datasetQueryExecutor.LastResultTruncated;
                }
            }
            else
            {
                result = await _queryExecutor.ExecuteAsync(
                    chart.ConnectionId!.Value, userId, chart.SqlQuery, ct);
                truncated = _queryExecutor.LastResultTruncated;
            }
        }
        finally
        {
            QueryConcurrency.Release();
        }

        var dataUpdatedAt = DateTimeOffset.UtcNow;
        var cacheTtl = ResolveCacheTtl(chart);

        if (cacheTtl is { } ttl)
        {
            try
            {
                await _chartCache.SetAsync(
                    userId,
                    id,
                    new CachedChartResult(userId, id, result, chart.UpdatedAt, dataUpdatedAt, truncated),
                    ttl,
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
        }

        _audit.Record(new AuditEntry(
            AuditAction.ChartExecuted,
            userId,
            chart.ConnectionId,
            chart.DatasetId,
            chart.Id,
            chart.ChartType,
            chart.SqlQuery,
            result.Count,
            "success",
            "query"));

        return BuildResponse(chart, result, dataUpdatedAt, truncated);
    }

    /// <summary>
    /// Connection results are short-lived; uploaded-dataset results use a longer
    /// TTL because they are expensive to recompute, but zero disables caching.
    /// </summary>
    private bool IsCacheEnabled(SavedChart chart) =>
        chart.ConnectionId is not null
            ? _cacheSettings.ConnectionEntryTtlMinutes > 0
            : _cacheSettings.DatasetEntryTtlMinutes > 0;

    private TimeSpan? ResolveCacheTtl(SavedChart chart)
    {
        if (chart.ConnectionId is not null)
        {
            return _cacheSettings.ConnectionEntryTtlMinutes > 0
                ? TimeSpan.FromMinutes(_cacheSettings.ConnectionEntryTtlMinutes)
                : null;
        }

        return _cacheSettings.DatasetEntryTtlMinutes > 0
            ? TimeSpan.FromMinutes(_cacheSettings.DatasetEntryTtlMinutes)
            : null;
    }

    private static readonly SemaphoreSlim QueryConcurrency = new(8, 8);

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

    private async Task<Guid?> ResolveCollectionIdAsync(SavedChart chart, CancellationToken ct)
    {
        if (chart.DatasetId is null) return null;

        return await _db.SavedDatasets
            .AsNoTracking()
            .Where(d => d.Id == chart.DatasetId.Value)
            .Select(d => (Guid?)d.CollectionId)
            .FirstOrDefaultAsync(ct);
    }

    private static ChartConfigResponse BuildResponse(
        SavedChart chart,
        List<Dictionary<string, object?>> rows,
        DateTimeOffset dataUpdatedAt,
        bool truncated = false)
    {
        // Saved charts may predate current axis handling; reconcile the axes
        // with the columns this execution returned without persisting changes.
        var warnings = new List<string>();
        if (truncated)
            warnings.Add("The result was truncated at the configured row limit.");

        var config = new AiChartConfig
        {
            ChartType = chart.ChartType,
            Title = chart.Title,
            XAxis = chart.XAxis,
            YAxis = [.. chart.YAxis],
            GroupBy = chart.GroupBy,
            Aggregation = chart.Aggregation,
            SqlQuery = chart.SqlQuery,
            DataModel = chart.DataModel,
            // Presentation params (topN) are stored with the style. The shaper
            // reads them from here, so execute returns exactly what the UI shows.
            StyleConfig = chart.StyleConfig,
        };

        ChartResultNormalizer.Normalize(config, rows, warnings);
        // Keep shaper notes out of the user-facing warning banner: a normal
        // top-N grouping is a presentation choice, not a data problem.
        var shapedRows = ChartResultShaper.Shape(config, rows);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            shapedRows,
            chart.StyleConfig,
            DataModel: chart.DataModel,
            DataUpdatedAt: dataUpdatedAt,
            Warnings: warnings.Count > 0 ? warnings : null,
            Truncated: truncated
        );
    }

    /// <summary>
    /// Rejects chart SQL that is not a safe SELECT before it is persisted. The
    /// query executor validates again at execution time; this closes the gap where
    /// a chart could be saved and executed later, including SQL Server writes such
    /// as <c>SELECT ... INTO</c>.
    /// </summary>
    /// <summary>
    /// Dataset charts store a structured query. Ground it against the uploaded
    /// file's columns before persisting so unknown columns cannot become silently
    /// empty charts.
    /// </summary>
    private static void EnsureGroundedDataModel(
        DataQueryModel dataModel,
        IReadOnlyList<string> columnNames)
    {
        var error = DataQueryModelGrounding.Validate(dataModel, columnNames);
        if (error is not null)
            throw new ArgumentException($"The chart data model is not valid: {error}");
    }

    private async Task EnsureSafeQueryAsync(
        Guid? connectionId,
        Guid? datasetId,
        string? sqlQuery,
        CancellationToken ct)
    {        if (string.IsNullOrWhiteSpace(sqlQuery))
            return;

        if (connectionId.HasValue)
        {
            var provider = await _db.ExternalConnections
                .AsNoTracking()
                .Where(ec => ec.Id == connectionId.Value)
                .Select(ec => (DbProvider?)ec.DbProvider)
                .FirstOrDefaultAsync(ct);

            if (provider is null)
                throw new InvalidOperationException("Connection not found.");

            if (!_sqlValidator.IsSelectOnly(sqlQuery, provider.Value, out var error))
                throw new ArgumentException($"The chart query is not allowed: {error}");

            return;
        }

        if (datasetId.HasValue && !SqliteSelectGuard.IsSelectOnly(sqlQuery, out var datasetError))
            throw new ArgumentException($"The chart query is not allowed: {datasetError}");
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
