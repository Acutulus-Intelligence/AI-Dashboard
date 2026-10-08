using Application.Common.Exceptions;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class DashboardService : IDashboardService
{
    private const int MaxTextContentLength = 5000;
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptionService;
    private readonly UserManager<User> _userManager;
    private readonly IConnectionAccessService _connectionAccess;
    private readonly ICollectionAccessService _collectionAccess;

    public DashboardService(
        IApplicationDbContext db,
        ISubscriptionService subscriptionService,
        UserManager<User> userManager,
        IConnectionAccessService connectionAccess,
        ICollectionAccessService collectionAccess)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _userManager = userManager;
        _connectionAccess = connectionAccess;
        _collectionAccess = collectionAccess;
    }

    public async Task<List<DashboardSummaryResponse>> GetDashboardsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Dashboards
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DashboardSummaryResponse(
                d.Id,
                d.Name,
                userId,
                d.Widgets.Count,
                d.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<DashboardResponse> GetDashboardAsync(Guid userId, Guid dashboardId, CancellationToken ct = default)
    {
        var dashboard = await EnsureOwnedAsync(userId, dashboardId, ct);
        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> CreateDashboardAsync(Guid userId, CreateDashboardRequest request, CancellationToken ct = default)
    {
        var limit = await _subscriptionService.GetMaxDashboardsAsync(userId, ct);
        if (limit.HasValue)
        {
            var count = await _db.Dashboards.CountAsync(d => d.UserId == userId, ct);
            if (count >= limit.Value)
                throw new ConflictException(
                    $"You have reached the limit of {limit.Value} dashboard{(limit.Value == 1 ? "" : "s")}.",
                    "dashboard_limit_reached");
        }

        var name = request.Name.Trim();
        var nameTaken = await _db.Dashboards.AnyAsync(d => d.UserId == userId && d.Name == name, ct);
        if (nameTaken)
            throw new ConflictException("You already have a dashboard with that name.", "dashboard_name_conflict");

        var dashboard = new Dashboard
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Dashboards.Add(dashboard);
        await _db.SaveChangesAsync(ct);

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> RenameDashboardAsync(Guid userId, Guid dashboardId, RenameDashboardRequest request, CancellationToken ct = default)
    {
        var dashboard = await EnsureOwnedAsync(userId, dashboardId, ct);

        var name = request.Name.Trim();
        var nameTaken = await _db.Dashboards
            .AnyAsync(d => d.UserId == userId && d.Name == name && d.Id != dashboardId, ct);
        if (nameTaken)
            throw new ConflictException("You already have a dashboard with that name.", "dashboard_name_conflict");

        dashboard.Name = name;
        dashboard.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToResponse(dashboard);
    }

    public async Task DeleteDashboardAsync(Guid userId, Guid dashboardId, CancellationToken ct = default)
    {
        var dashboard = await EnsureOwnedAsync(userId, dashboardId, ct);

        _db.DashboardWidgets.RemoveRange(dashboard.Widgets);
        _db.Dashboards.Remove(dashboard);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DashboardResponse> SaveWidgetsAsync(Guid userId, Guid dashboardId, SaveWidgetsRequest request, CancellationToken ct = default)
    {
        var dashboard = await EnsureOwnedAsync(userId, dashboardId, ct);

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

    public async Task<TransferDashboardResponse> TransferOwnershipAsync(Guid userId, Guid dashboardId, TransferDashboardRequest request, CancellationToken ct = default)
    {
        var dashboard = await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Dashboard not found.");

        if (request.NewOwnerId == userId)
            throw new InvalidOperationException("You already own this dashboard.");

        var owner = await _db.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("Owner not found.");

        if (owner.CompanyId is null)
            throw new InvalidOperationException(
                "Dashboards can only be transferred between members of the same company.");

        var identityOwner = await _userManager.FindByIdAsync(userId.ToString());
        if (identityOwner is null || !await _userManager.CheckPasswordAsync(identityOwner, request.CurrentPassword))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var newOwner = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.NewOwnerId && u.CompanyId == owner.CompanyId, ct)
            ?? throw new KeyNotFoundException("New owner must be a member of your company.");

        var chartIds = dashboard.Widgets
            .Where(w => w.WidgetType == WidgetType.Chart && w.SavedChartId.HasValue)
            .Select(w => w.SavedChartId!.Value)
            .Distinct()
            .ToList();

        var charts = chartIds.Count > 0
            ? await _db.SavedCharts
                .Where(sc => chartIds.Contains(sc.Id) && sc.UserId == userId)
                .ToListAsync(ct)
            : [];

        // The new owner must be able to read every data source backing the dashboard's
        // charts, otherwise the transferred charts render without data.
        var requiresSharing = new List<TransferDataSourceItem>();
        var connectionsToShare = await ResolveConnectionsToShareAsync(charts, newOwner.Id, userId, requiresSharing, ct);
        var collectionsToShare = await ResolveCollectionsToShareAsync(charts, newOwner.Id, userId, requiresSharing, ct);

        if (requiresSharing.Count > 0 && !request.ShareDataSources)
            return new TransferDashboardResponse(false, requiresSharing);

        foreach (var connection in connectionsToShare)
        {
            connection.CompanyId = owner.CompanyId.Value;
            connection.Visibility = ConnectionVisibility.Company;
            connection.AllowedRoleIds = [];
        }

        foreach (var collection in collectionsToShare)
        {
            collection.CompanyId = owner.CompanyId.Value;
            collection.Visibility = CollectionVisibility.Company;
            collection.AllowedRoleIds = [];
        }

        dashboard.UserId = newOwner.Id;
        dashboard.UpdatedAt = DateTime.UtcNow;

        if (charts.Count > 0)
        {
            // Charts still referenced by another of the seller's dashboards stay put;
            // the transferred dashboard gets its own copies so no widget ever points
            // at a chart owned by someone else.
            var sharedChartIds = (await _db.DashboardWidgets
                .Where(w => w.SavedChartId != null
                    && chartIds.Contains(w.SavedChartId!.Value)
                    && w.DashboardId != dashboard.Id)
                .Select(w => w.SavedChartId!.Value)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet();

            var takenTitles = (await _db.SavedCharts
                .Where(sc => sc.UserId == newOwner.Id)
                .Select(sc => sc.Title)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var chart in charts)
            {
                if (sharedChartIds.Contains(chart.Id))
                {
                    var copy = new SavedChart
                    {
                        Id = Guid.NewGuid(),
                        UserId = newOwner.Id,
                        Title = ResolveUniqueTitle(chart.Title, takenTitles),
                        ChartType = chart.ChartType,
                        XAxis = chart.XAxis,
                        YAxis = chart.YAxis.ToArray(),
                        Aggregation = chart.Aggregation,
                        GroupBy = chart.GroupBy,
                        SqlQuery = chart.SqlQuery,
                        DataModel = chart.DataModel,
                        ConnectionId = chart.ConnectionId,
                        DatasetId = chart.DatasetId,
                        TableName = chart.TableName,
                        StyleConfig = chart.StyleConfig,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    _db.SavedCharts.Add(copy);

                    foreach (var widget in dashboard.Widgets.Where(w => w.SavedChartId == chart.Id))
                        widget.SavedChartId = copy.Id;
                }
                else
                {
                    chart.UserId = newOwner.Id;
                    chart.UpdatedAt = DateTime.UtcNow;
                    chart.Title = ResolveUniqueTitle(chart.Title, takenTitles);
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new TransferDashboardResponse(true, []);
    }

    private async Task<List<ExternalConnection>> ResolveConnectionsToShareAsync(
        List<SavedChart> charts,
        Guid newOwnerId,
        Guid sellerId,
        List<TransferDataSourceItem> requiresSharing,
        CancellationToken ct)
    {
        var connectionIds = charts
            .Where(c => c.ConnectionId.HasValue)
            .Select(c => c.ConnectionId!.Value)
            .Distinct()
            .ToList();

        if (connectionIds.Count == 0)
            return [];

        var connections = await _db.ExternalConnections
            .Where(ec => connectionIds.Contains(ec.Id))
            .ToListAsync(ct);

        var toShare = new List<ExternalConnection>();
        foreach (var connection in connections)
        {
            if (await _connectionAccess.CanViewAsync(connection.Id, newOwnerId, ct))
                continue;

            if (!await _connectionAccess.CanManageAsync(connection.Id, sellerId, ct))
                throw new InvalidOperationException(
                    $"\"{connection.Name}\" is not accessible to the new owner and you cannot share it. Ask a company owner to make it available first.");

            toShare.Add(connection);
            requiresSharing.Add(new TransferDataSourceItem("connection", connection.Id, connection.Name));
        }

        return toShare;
    }

    private async Task<List<DataCollection>> ResolveCollectionsToShareAsync(
        List<SavedChart> charts,
        Guid newOwnerId,
        Guid sellerId,
        List<TransferDataSourceItem> requiresSharing,
        CancellationToken ct)
    {
        var datasetIds = charts
            .Where(c => c.DatasetId.HasValue)
            .Select(c => c.DatasetId!.Value)
            .Distinct()
            .ToList();

        if (datasetIds.Count == 0)
            return [];

        var collectionIds = await _db.SavedDatasets
            .Where(ds => datasetIds.Contains(ds.Id))
            .Select(ds => ds.CollectionId)
            .Distinct()
            .ToListAsync(ct);

        if (collectionIds.Count == 0)
            return [];

        var collections = await _db.DataCollections
            .Where(c => collectionIds.Contains(c.Id))
            .ToListAsync(ct);

        var toShare = new List<DataCollection>();
        foreach (var collection in collections)
        {
            if (await _collectionAccess.CanViewAsync(collection.Id, newOwnerId, ct))
                continue;

            if (!await _collectionAccess.CanManageAsync(collection.Id, sellerId, ct))
                throw new InvalidOperationException(
                    $"\"{collection.Name}\" is not accessible to the new owner and you cannot share it. Ask a company owner to make it available first.");

            toShare.Add(collection);
            requiresSharing.Add(new TransferDataSourceItem("collection", collection.Id, collection.Name));
        }

        return toShare;
    }

    private async Task<Dashboard> EnsureOwnedAsync(Guid userId, Guid dashboardId, CancellationToken ct)
    {
        return await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Dashboard not found.");
    }

    private static string ResolveUniqueTitle(string title, HashSet<string> takenTitles)
    {
        if (takenTitles.Add(title))
            return title;

        var suffix = 2;
        string candidate;
        do
        {
            candidate = $"{title} ({suffix++})";
        } while (!takenTitles.Add(candidate));

        return candidate;
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
            dashboard.UserId ?? Guid.Empty,
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
