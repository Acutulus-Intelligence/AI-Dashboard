using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken ct = default);
    Task<DashboardResponse> SaveWidgetsAsync(Guid userId, SaveWidgetsRequest request, CancellationToken ct = default);
}
