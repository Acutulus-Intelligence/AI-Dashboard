using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IExternalConnectionService
{
    Task<ConnectionResponse> CreateAsync(Guid userId, CreateConnectionRequest request, CancellationToken ct = default);
    Task<List<ConnectionResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<ConnectionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default);
}
