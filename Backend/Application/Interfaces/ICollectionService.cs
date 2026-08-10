using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface ICollectionService
{
    Task<CollectionResponse> CreateAsync(Guid userId, CreateCollectionRequest request, CancellationToken ct = default);
    Task<List<CollectionResponse>> GetCollectionsAsync(Guid userId, CancellationToken ct = default);
    Task<CollectionDetailResponse> GetCollectionAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<CollectionResponse> UpdateAsync(Guid id, Guid userId, UpdateCollectionRequest request, CancellationToken ct = default);
    Task DeleteCollectionAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<CollectionFileResponse> UploadFileAsync(Guid collectionId, Guid userId, string fileName, Stream fileStream, CancellationToken ct = default);
    Task<CollectionFileDetailResponse> GetFileAsync(Guid collectionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task DeleteFileAsync(Guid collectionId, Guid fileId, Guid userId, CancellationToken ct = default);

    Task<ChartConfigResponse> GenerateChartAsync(Guid collectionId, Guid fileId, Guid userId, GenerateCollectionChartRequest request, CancellationToken ct = default);
}