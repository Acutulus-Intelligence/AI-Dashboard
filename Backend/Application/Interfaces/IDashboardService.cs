using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IDashboardService
{
    Task<List<DashboardSummaryResponse>> GetDashboardsAsync(Guid userId, CancellationToken ct = default);
    Task<DashboardResponse> GetDashboardAsync(Guid userId, Guid dashboardId, CancellationToken ct = default);
    Task<DashboardResponse> CreateDashboardAsync(Guid userId, CreateDashboardRequest request, CancellationToken ct = default);
    Task<DashboardResponse> RenameDashboardAsync(Guid userId, Guid dashboardId, RenameDashboardRequest request, CancellationToken ct = default);
    Task DeleteDashboardAsync(Guid userId, Guid dashboardId, CancellationToken ct = default);
    Task<DashboardResponse> SaveWidgetsAsync(Guid userId, Guid dashboardId, SaveWidgetsRequest request, CancellationToken ct = default);
    Task<TransferDashboardResponse> TransferOwnershipAsync(Guid userId, Guid dashboardId, TransferDashboardRequest request, CancellationToken ct = default);
}
