using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class DashboardService : IDashboardService
{
    private const int MaxTextContentLength = 5000;
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

        await ValidateChartOwnershipAsync(userId, request.Widgets, ct);
        ValidateTextWidgets(request.Widgets);

        var incomingIds = request.Widgets
            .Where(w => w.Id.HasValue)
            .Select(w => w.Id!.Value)
            .ToHashSet();

        var widgetsToRemove = dashboard.Widgets
            .Where(w => !incomingIds.Contains(w.Id))
            .ToList();

        if (widgetsToRemove.Count > 0)
            _db.DashboardWidgets.RemoveRange(widgetsToRemove);

        var existingById = dashboard.Widgets.ToDictionary(w => w.Id);

        foreach (var item in request.Widgets)
        {
            if (item.Id.HasValue && existingById.TryGetValue(item.Id.Value, out var existing))
            {
                ApplyWidgetItem(existing, item);
                continue;
            }

            _db.DashboardWidgets.Add(CreateWidget(dashboard.Id, item));
        }

        dashboard.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var updated = await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstAsync(d => d.Id == dashboard.Id, ct);

        return MapToResponse(updated);
    }

    private static void ApplyWidgetItem(DashboardWidget existing, WidgetItem item)
    {
        existing.WidgetType = item.WidgetType;
        existing.SavedChartId = item.WidgetType == WidgetType.Chart ? item.SavedChartId : null;
        existing.TextContent = item.WidgetType == WidgetType.Text ? item.TextContent : null;
        existing.TextVariant = item.WidgetType == WidgetType.Text ? item.TextStyle : null;
        existing.TextHorizontalAlign = item.WidgetType == WidgetType.Text ? item.HorizontalAlign : null;
        existing.TextVerticalAlign = item.WidgetType == WidgetType.Text ? item.VerticalAlign : null;
        existing.PositionX = item.PositionX;
        existing.PositionY = item.PositionY;
        existing.Width = item.Width;
        existing.Height = item.Height;
    }

    private static DashboardWidget CreateWidget(Guid dashboardId, WidgetItem item)
    {
        return new DashboardWidget
        {
            Id = item.Id ?? Guid.NewGuid(),
            DashboardId = dashboardId,
            WidgetType = item.WidgetType,
            SavedChartId = item.WidgetType == WidgetType.Chart ? item.SavedChartId : null,
            TextContent = item.WidgetType == WidgetType.Text ? item.TextContent : null,
            TextVariant = item.WidgetType == WidgetType.Text ? item.TextStyle : null,
            TextHorizontalAlign = item.WidgetType == WidgetType.Text ? item.HorizontalAlign : null,
            TextVerticalAlign = item.WidgetType == WidgetType.Text ? item.VerticalAlign : null,
            PositionX = item.PositionX,
            PositionY = item.PositionY,
            Width = item.Width,
            Height = item.Height,
        };
    }

    private async Task ValidateChartOwnershipAsync(Guid userId, List<WidgetItem> widgets, CancellationToken ct)
    {
        var chartIds = widgets
            .Where(w => w.WidgetType == WidgetType.Chart && w.SavedChartId.HasValue)
            .Select(w => w.SavedChartId!.Value)
            .Distinct()
            .ToList();

        if (chartIds.Count == 0)
            return;

        var ownedCount = await _db.SavedCharts
            .CountAsync(sc => chartIds.Contains(sc.Id) && sc.UserId == userId, ct);

        if (ownedCount != chartIds.Count)
            throw new UnauthorizedAccessException("One or more charts do not belong to you.");
    }

    private static void ValidateTextWidgets(List<WidgetItem> widgets)
    {
        foreach (var widget in widgets.Where(w => w.WidgetType == WidgetType.Text))
        {
            if (widget.TextContent is not null && widget.TextContent.Length > MaxTextContentLength)
                throw new ArgumentException($"Text content must be {MaxTextContentLength} characters or fewer.");
        }
    }

    private static DashboardResponse MapToResponse(Dashboard dashboard)
    {
        return new DashboardResponse(
            dashboard.Id,
            dashboard.Name,
            dashboard.Widgets.Select(w => new DashboardWidgetItem(
                w.Id,
                w.WidgetType,
                w.SavedChartId,
                w.TextContent,
                w.TextVariant,
                w.TextHorizontalAlign,
                w.TextVerticalAlign,
                w.WidgetType == WidgetType.Chart ? w.SavedChart?.Title ?? "Unknown" : null,
                w.WidgetType == WidgetType.Chart ? w.SavedChart?.ChartType ?? "bar" : null,
                w.PositionX,
                w.PositionY,
                w.Width,
                w.Height
            )).ToList()
        );
    }
}
