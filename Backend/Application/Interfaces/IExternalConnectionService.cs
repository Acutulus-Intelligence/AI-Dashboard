using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IExternalConnectionService
{
    Task<ConnectionResponse> CreateAsync(Guid userId, CreateConnectionRequest request, CancellationToken ct = default);
    Task<List<ConnectionResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetCompanyConnectionCountAsync(Guid userId, CancellationToken ct = default);
    Task<ParseConnectionStringResponse> ParseConnectionStringAsync(string connectionString, CancellationToken ct = default);
    Task<ConnectionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ConnectionConfigResponse> GetConfigAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ConnectionResponse> UpdateAsync(Guid id, Guid userId, UpdateConnectionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default);
}
