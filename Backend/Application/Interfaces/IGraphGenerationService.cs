using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IGraphGenerationService
{
    Task<ChartConfigResponse> GenerateAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default);
    Task<ChartConfigResponse> ManualAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default);
}
