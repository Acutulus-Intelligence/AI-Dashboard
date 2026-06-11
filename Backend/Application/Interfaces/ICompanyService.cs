using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface ICompanyService
{
    Task<CompanyResponse> CreateAsync(Guid ownerId, string name, CancellationToken ct = default);
    Task<CompanyResponse> GetByIdAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<CompanyResponse> GetMyCompanyAsync(Guid userId, CancellationToken ct = default);
    Task<List<CompanyUserResponse>> GetUsersAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task UpdateUserRoleAsync(Guid companyId, Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default);
    Task TransferOwnershipAsync(Guid companyId, Guid newOwnerId, Guid currentOwnerId, CancellationToken ct = default);
    Task<string> InviteUserAsync(Guid companyId, string email, Guid roleId, Guid actorId, CancellationToken ct = default);
    Task AcceptInviteAsync(string token, Guid userId, CancellationToken ct = default);
    Task AcceptInviteByIdAsync(Guid inviteId, Guid userId, CancellationToken ct = default);
    Task RemoveUserAsync(Guid companyId, Guid userIdToRemove, Guid actorId, CancellationToken ct = default);
    Task<List<CompanyRoleResponse>> GetRolesAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<CompanyRoleResponse> CreateRoleAsync(Guid companyId, CreateRoleRequest request, Guid actorId, CancellationToken ct = default);
    Task<CompanyRoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, Guid actorId, CancellationToken ct = default);
    Task<List<CompanyInviteResponse>> GetInvitesAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<List<CompanyInviteResponse>> GetPendingInvitesAsync(string email, CancellationToken ct = default);
    Task RevokeInviteAsync(Guid companyId, Guid inviteId, Guid actorId, CancellationToken ct = default);
    Task RejectInviteAsync(Guid inviteId, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid companyId, Guid actorId, CancellationToken ct = default);
}
