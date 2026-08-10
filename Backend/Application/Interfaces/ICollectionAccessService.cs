using Domain.Models;

namespace Application.Interfaces;

public interface ICollectionAccessService
{
    Task<DataCollection?> FindViewableAsync(Guid collectionId, Guid userId, CancellationToken ct = default);
    Task<bool> CanViewAsync(Guid collectionId, Guid userId, CancellationToken ct = default);
    Task<DataCollection?> FindManageableAsync(Guid collectionId, Guid userId, CancellationToken ct = default);
    Task<bool> CanManageAsync(Guid collectionId, Guid userId, CancellationToken ct = default);
    Task<bool> HasCollectionManagePermissionAsync(Guid userId, CancellationToken ct = default);
}