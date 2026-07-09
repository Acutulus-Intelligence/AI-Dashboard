using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;

    public DashboardService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var dashboard = await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (dashboard is null)
        {
            dashboard = new Dashboard
            {
                Id = Guid.NewGuid(),
                Name = "My Dashboard",
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Dashboards.Add(dashboard);
            await _db.SaveChangesAsync(ct);
        }

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> SaveWidgetsAsync(Guid userId, SaveWidgetsRequest request, CancellationToken ct = default)
    {
        var dashboard = await _db.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Dashboard not found.");

        _db.DashboardWidgets.RemoveRange(dashboard.Widgets);

        var widgets = request.Widgets.Select(w => new DashboardWidget
        {
            Id = Guid.NewGuid(),
            DashboardId = dashboard.Id,
            SavedChartId = w.SavedChartId,
            PositionX = w.PositionX,
            PositionY = w.PositionY,
            Width = w.Width,
            Height = w.Height,
        }).ToList();

        _db.DashboardWidgets.AddRange(widgets);
        dashboard.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Reload dashboard with chart info
        var updated = await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstAsync(d => d.Id == dashboard.Id);

        return MapToResponse(updated);
    }

    private static DashboardResponse MapToResponse(Dashboard dashboard)
    {
        return new DashboardResponse(
            dashboard.Id,
            dashboard.Name,
            dashboard.Widgets.Select(w => new DashboardWidgetItem(
                w.Id,
                w.SavedChartId,
                w.SavedChart?.Title ?? "Unknown",
                w.SavedChart?.ChartType ?? "bar",
                w.PositionX,
                w.PositionY,
                w.Width,
                w.Height
            )).ToList()
        );
    }
}
