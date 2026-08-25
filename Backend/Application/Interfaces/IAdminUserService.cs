using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IAdminUserService
{
    Task<List<AdminUserResponse>> GetUsersAsync(string? search = null, int? take = null, bool staffOnly = false, CancellationToken ct = default);
    Task<AdminUserResponse> SetModeratorRoleAsync(Guid actorId, Guid userId, bool isModerator, CancellationToken ct = default);
    Task<AdminUserResponse> TransferAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default);
    Task<AdminUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default);
}
