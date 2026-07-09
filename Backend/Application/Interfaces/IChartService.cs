using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IChartService
{
    Task<ChartResponse> SaveChartAsync(Guid userId, SaveChartRequest request, CancellationToken ct = default);
    Task<List<ChartResponse>> GetChartsAsync(Guid userId, CancellationToken ct = default);
    Task<ChartDetailResponse> GetChartAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task DeleteChartAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ChartConfigResponse> ExecuteChartAsync(Guid id, Guid userId, CancellationToken ct = default);
}
