using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IDatasetService
{
    Task<DatasetResponse> UploadAsync(Guid userId, string fileName, Stream fileStream, CancellationToken ct = default);
    Task<List<DatasetResponse>> GetDatasetsAsync(Guid userId, CancellationToken ct = default);
    Task<DatasetDetailResponse> GetDatasetAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task DeleteDatasetAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ChartConfigResponse> GenerateChartAsync(Guid id, Guid userId, GenerateDatasetChartRequest request, CancellationToken ct = default);
}