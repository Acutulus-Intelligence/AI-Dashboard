using Application.Dtos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ISubscriptionService _subscriptionService;

    public CompanyController(ICompanyService companyService, ISubscriptionService subscriptionService)
    {
        _companyService = companyService;
        _subscriptionService = subscriptionService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _companyService.CreateAsync(userId, request.Name, ct);
        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCompany(CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _companyService.GetMyCompanyAsync(userId, ct);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _companyService.GetByIdAsync(id, userId, ct);
        return Ok(response);
    }

    [HttpGet("{id:guid}/users")]
    public async Task<IActionResult> GetUsers(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var users = await _companyService.GetUsersAsync(id, userId, ct);
        return Ok(users);
    }

    [HttpPut("{id:guid}/users/{userId:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid id, Guid userId, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _companyService.UpdateUserRoleAsync(id, userId, request.RoleId, actorId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-ownership/{userId:guid}")]
    public async Task<IActionResult> TransferOwnership(Guid id, Guid userId, CancellationToken ct)
    {
        var currentOwnerId = GetUserId();
        await _companyService.TransferOwnershipAsync(id, userId, currentOwnerId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> InviteUser(Guid id, [FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        var token = await _companyService.InviteUserAsync(id, request.Email, request.RoleId, actorId, ct);
        return Ok(new { token });
    }

    [HttpPost("accept-invite")]
    [AllowSubscriptionBypass]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        await _companyService.AcceptInviteByIdAsync(request.InviteId, userId, ct);

        try
        {
            await _subscriptionService.CancelUserSubscriptionAsync(userId, ct);
        }
        catch
        {
            // User may not have an individual subscription — that's fine.
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    public async Task<IActionResult> RemoveUser(Guid id, Guid userId, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _companyService.RemoveUserAsync(id, userId, actorId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/roles")]
    public async Task<IActionResult> GetRoles(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var roles = await _companyService.GetRolesAsync(id, userId, ct);
        return Ok(roles);
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> CreateRole(Guid id, [FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        var role = await _companyService.CreateRoleAsync(id, request, actorId, ct);
        return Ok(role);
    }

    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, Guid roleId, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        var role = await _companyService.UpdateRoleAsync(roleId, request, actorId, ct);
        return Ok(role);
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id, Guid roleId, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _companyService.DeleteRoleAsync(roleId, actorId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _companyService.DeleteAsync(id, actorId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/invites")]
    public async Task<IActionResult> GetInvites(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var invites = await _companyService.GetInvitesAsync(id, userId, ct);
        return Ok(invites);
    }

    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid id, Guid inviteId, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _companyService.RevokeInviteAsync(id, inviteId, actorId, ct);
        return NoContent();
    }

    [HttpGet("~/api/invites/pending")]
    [AllowSubscriptionBypass]
    public async Task<IActionResult> GetPendingInvites(CancellationToken ct)
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (email is null)
            throw new UnauthorizedAccessException("Email not found in token.");

        var invites = await _companyService.GetPendingInvitesAsync(email, ct);
        return Ok(invites);
    }

    [HttpDelete("~/api/invites/{inviteId:guid}")]
    [AllowSubscriptionBypass]
    public async Task<IActionResult> RejectInvite(Guid inviteId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _companyService.RejectInviteAsync(inviteId, userId, ct);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}
