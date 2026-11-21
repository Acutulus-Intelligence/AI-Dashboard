using Application.Caching;
using Application.Common.Data;
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
    private const int MaxNameLength = 200;
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptionService;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentPasswordVerifier _currentPasswordVerifier;
    private readonly IConnectionAccessService _connectionAccess;
    private readonly ICollectionAccessService _collectionAccess;
    private readonly IChartCacheInvalidator _chartCacheInvalidator;

    public DashboardService(
        IApplicationDbContext db,
        ISubscriptionService subscriptionService,
        UserManager<User> userManager,
        ICurrentPasswordVerifier currentPasswordVerifier,
        IConnectionAccessService connectionAccess,
        ICollectionAccessService collectionAccess,
        IChartCacheInvalidator chartCacheInvalidator)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _userManager = userManager;
        _currentPasswordVerifier = currentPasswordVerifier;
        _connectionAccess = connectionAccess;
        _collectionAccess = collectionAccess;
        _chartCacheInvalidator = chartCacheInvalidator;
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
        await EnsureUniqueNameAsync(userId, name, excludeId: null, ct);

        var dashboard = new Dashboard
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Dashboards.Add(dashboard);
        await SaveDashboardAsync(ct);

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> RenameDashboardAsync(Guid userId, Guid dashboardId, RenameDashboardRequest request, CancellationToken ct = default)
    {
        var dashboard = await EnsureOwnedAsync(userId, dashboardId, ct);

        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(userId, name, excludeId: dashboardId, ct);

        dashboard.Name = name;
        dashboard.UpdatedAt = DateTime.UtcNow;
        await SaveDashboardAsync(ct);

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

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            // A chart or the dashboard itself was deleted between validation and save.
            // Report the stale state instead of letting the FK violation become a 500.
            if (!await _db.Dashboards.AnyAsync(d => d.Id == dashboardId && d.UserId == userId, ct))
                throw new KeyNotFoundException("Dashboard not found.");

            throw new ConflictException(
                "A chart on this dashboard was deleted. Reload the dashboard and try again.",
                "chart_deleted");
        }

        var updated = await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstAsync(d => d.Id == dashboard.Id, ct);

        return MapToResponse(updated);
    }

    public async Task<DashboardResponse> UpdateSettingsAsync(Guid userId, Guid dashboardId, UpdateDashboardSettingsRequest request, CancellationToken ct = default)
    {
        var dashboard = await LoadDashboardAsync(dashboardId, ct);
        await EnsureCanManagePollingSettingsAsync(userId, dashboard, ct);

        dashboard.AutoRefreshEnabled = request.AutoRefreshEnabled;
        await _db.SaveChangesAsync(ct);

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> UpdateWidgetRefreshSettingAsync(
        Guid userId,
        Guid dashboardId,
        Guid widgetId,
        UpdateWidgetRefreshSettingRequest request,
        CancellationToken ct = default)
    {
        var dashboard = await LoadDashboardAsync(dashboardId, ct);
        await EnsureCanManagePollingSettingsAsync(userId, dashboard, ct);

        var widget = dashboard.Widgets.FirstOrDefault(w => w.Id == widgetId)
            ?? throw new KeyNotFoundException("Widget not found.");

        if (widget.WidgetType != WidgetType.Chart)
            throw new InvalidOperationException("Auto-refresh is only available for chart widgets.");

        widget.AutoRefreshEnabled = request.AutoRefreshEnabled;
        await _db.SaveChangesAsync(ct);

        return MapToResponse(dashboard);
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

        var identityOwner = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("Owner not found.");

        var passwordCheck = await _currentPasswordVerifier.VerifyAsync(identityOwner, request.CurrentPassword);
        if (passwordCheck == CurrentPasswordCheck.LockedOut)
            throw new ArgumentException("Too many failed attempts. Try again later.");
        if (passwordCheck != CurrentPasswordCheck.Success)
            throw new ArgumentException("Current password is incorrect.");

        var newOwner = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.NewOwnerId && u.CompanyId == owner.CompanyId, ct)
            ?? throw new KeyNotFoundException("New owner must be a member of your company.");

        var limit = await _subscriptionService.GetMaxDashboardsAsync(newOwner.Id, ct);
        if (limit.HasValue)
        {
            var count = await _db.Dashboards.CountAsync(d => d.UserId == newOwner.Id, ct);
            if (count >= limit.Value)
                throw new ConflictException(
                    $"The new owner has reached the limit of {limit.Value} dashboard{(limit.Value == 1 ? "" : "s")}.",
                    "dashboard_limit_reached");
        }

        var chartIds = dashboard.Widgets
            .Where(w => w.WidgetType == WidgetType.Chart && w.SavedChartId.HasValue)
            .Select(w => w.SavedChartId!.Value)
            .Distinct()
            .ToList();

        var takenDashboardNames = (await _db.Dashboards
            .Where(d => d.UserId == newOwner.Id)
            .Select(d => d.Name)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        // Grant the new owner access through their company role only, so the
        // sources stay hidden from every other company member. Existing role
        // grants are preserved.
        if (connectionsToShare.Count > 0 || collectionsToShare.Count > 0)
        {
            var recipientRoleId = newOwner.CompanyRoleId
                ?? throw new InvalidOperationException(
                    $"\"{newOwner.Email}\" has no company role. Assign them a role before sharing data sources.");

            foreach (var connection in connectionsToShare)
            {
                connection.CompanyId = owner.CompanyId.Value;
                connection.Visibility = ConnectionVisibility.Roles;
                if (!connection.AllowedRoleIds.Contains(recipientRoleId))
                    connection.AllowedRoleIds.Add(recipientRoleId);
            }

            foreach (var collection in collectionsToShare)
            {
                collection.CompanyId = owner.CompanyId.Value;
                collection.Visibility = CollectionVisibility.Roles;
                if (!collection.AllowedRoleIds.Contains(recipientRoleId))
                    collection.AllowedRoleIds.Add(recipientRoleId);
            }
        }

        dashboard.Name = ResolveUniqueName(dashboard.Name, takenDashboardNames);
        dashboard.UserId = newOwner.Id;
        dashboard.UpdatedAt = DateTime.UtcNow;

        // Moved charts change owner, so the previous owner's cached result for that
        // chart id becomes unreachable; drop it instead of waiting for its TTL.
        var staleCacheTargets = new List<ChartCacheTarget>();

        if (charts.Count > 0)
        {
            // Charts still referenced by another of the seller's dashboards stay put;
            // the transferred dashboard gets its own copies so no widget ever points
            // at a chart owned by someone else.
            var sharedChartIds = (await _db.DashboardWidgets
                .Where(w => w.SavedChartId != null
                    && chartIds.Contains(w.SavedChartId!.Value)
                    && w.DashboardId != dashboard.Id
                    && w.Dashboard.UserId == userId)
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
                        Title = ResolveUniqueName(chart.Title, takenTitles),
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
                    chart.FolderId = null;
                    chart.UpdatedAt = DateTime.UtcNow;
                    chart.Title = ResolveUniqueName(chart.Title, takenTitles);
                    staleCacheTargets.Add(new ChartCacheTarget(userId, chart.Id));
                }
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent create by the new owner claimed one of the names resolved above.
            throw new ConflictException(
                "The transfer could not be completed because a name is already in use. Please try again.",
                "dashboard_name_conflict");
        }

        if (staleCacheTargets.Count > 0)
            await _chartCacheInvalidator.InvalidateChartsAsync(staleCacheTargets, ct);

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

    private async Task<Dashboard> LoadDashboardAsync(Guid dashboardId, CancellationToken ct)
    {
        return await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstOrDefaultAsync(d => d.Id == dashboardId, ct)
            ?? throw new KeyNotFoundException("Dashboard not found.");
    }

    private async Task EnsureCanManagePollingSettingsAsync(Guid userId, Dashboard dashboard, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        // Individual accounts manage their own dashboards.
        if (user.UserType != UserType.Company)
        {
            if (dashboard.UserId != userId)
                throw new ForbiddenException("You do not have permission to change refresh settings for this dashboard.");
            return;
        }

        // Company dashboards are controlled by the company owner only.
        if (user.CompanyId is null)
            throw new ForbiddenException("Only the company owner can change refresh settings.");

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == user.CompanyId.Value, ct);

        if (company is null || company.OwnerId != userId)
            throw new ForbiddenException("Only the company owner can change refresh settings.");

        if (dashboard.UserId == userId)
            return;

        var dashboardOwnerCompanyId = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == dashboard.UserId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (dashboardOwnerCompanyId != user.CompanyId)
            throw new ForbiddenException("You do not have permission to change refresh settings for this dashboard.");
    }

    private async Task<Dashboard> EnsureOwnedAsync(Guid userId, Guid dashboardId, CancellationToken ct)
    {
        return await _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.SavedChart)
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Dashboard not found.");
    }

    private static string ResolveUniqueName(string name, HashSet<string> takenNames)
    {
        // Dashboards.Name and SavedChart.Title are both limited to 200 characters.
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        if (takenNames.Add(name))
            return name;

        var suffix = 2;
        string candidate;
        do
        {
            var suffixText = $" ({suffix++})";
            var maxBaseLength = MaxNameLength - suffixText.Length;
            var baseName = name.Length > maxBaseLength ? name[..maxBaseLength] : name;
            candidate = $"{baseName}{suffixText}";
        } while (!takenNames.Add(candidate));

        return candidate;
    }

    private async Task EnsureUniqueNameAsync(
        Guid userId, string name, Guid? excludeId, CancellationToken ct)
    {
        var normalized = name.ToLower();
        var taken = await _db.Dashboards.AnyAsync(
            d => d.UserId == userId
                && d.Name.ToLower() == normalized
                && (!excludeId.HasValue || d.Id != excludeId.Value),
            ct);

        if (taken)
        {
            throw new ConflictException(
                "You already have a dashboard with that name.",
                "dashboard_name_conflict");
        }
    }

    private async Task SaveDashboardAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent create/rename hit the unique (UserId, Name) index.
            throw new ConflictException("You already have a dashboard with that name.", "dashboard_name_conflict");
        }
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
                w.Height,
                w.WidgetType == WidgetType.Chart ? w.AutoRefreshEnabled : null
            )).ToList(),
            dashboard.AutoRefreshEnabled
        );
    }
}
