using Domain.Models;

namespace Application.Interfaces;

public interface IConnectionAccessService
{
    Task<ExternalConnection?> FindViewableAsync(Guid connectionId, Guid userId, CancellationToken ct = default);
    Task<bool> CanViewAsync(Guid connectionId, Guid userId, CancellationToken ct = default);
    Task<bool> CanManageAsync(Guid connectionId, Guid userId, CancellationToken ct = default);
    Task<bool> HasConnectionManagePermissionAsync(Guid userId, CancellationToken ct = default);
}
