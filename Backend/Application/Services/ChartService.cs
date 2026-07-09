using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ChartService : IChartService
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queryExecutor;

    public ChartService(IApplicationDbContext db, IQueryExecutor queryExecutor)
    {
        _db = db;
        _queryExecutor = queryExecutor;
    }

    public async Task<ChartResponse> SaveChartAsync(Guid userId, SaveChartRequest request, CancellationToken ct = default)
    {
        var chart = new SavedChart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            ChartType = request.ChartType,
            XAxis = request.XAxis,
            YAxis = request.YAxis.ToArray(),
            Aggregation = request.Aggregation,
            GroupBy = request.GroupBy,
            SqlQuery = request.SqlQuery,
            ConnectionId = request.ConnectionId,
            TableName = request.TableName,
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
            chart.ConnectionId, chart.TableName, chart.CreatedAt
        );
    }

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

        if (chart.ConnectionId is null)
            throw new InvalidOperationException("Chart has no associated database connection.");

        var result = await _queryExecutor.ExecuteAsync(chart.ConnectionId.Value, userId, chart.SqlQuery, ct);

        return new ChartConfigResponse(
            chart.ChartType,
            chart.Title,
            chart.XAxis,
            [.. chart.YAxis],
            chart.Aggregation,
            chart.GroupBy,
            chart.SqlQuery,
            result
        );
    }
}
