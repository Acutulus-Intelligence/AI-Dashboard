using Application.DTos.Response;

namespace Application.Interfaces;

public interface IChartRecommendationService
{
    Task<ChartRecommendationResponse> RecommendForTableAsync(
        Guid connectionId,
        string tableName,
        Guid userId,
        CancellationToken ct = default);

    Task<ChartRecommendationResponse> RecommendForDatasetAsync(
        Guid collectionId,
        Guid fileId,
        Guid userId,
        CancellationToken ct = default);
}
