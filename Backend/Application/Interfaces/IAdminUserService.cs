using Application.DTos.Response;

namespace Application.Interfaces;

public interface IAdminUserService
{
    Task<List<AdminUserResponse>> GetUsersAsync(string? search = null, int? take = null, CancellationToken ct = default);
    Task<AdminUserResponse> SetAdminRoleAsync(Guid actorId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
