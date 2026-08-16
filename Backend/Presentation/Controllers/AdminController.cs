using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Moderator")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAdminUserService _adminUserService;

    public AdminController(IAdminService adminService, IAdminUserService adminUserService)
    {
        _adminService = adminService;
        _adminUserService = adminUserService;
    }

    [HttpGet("subscription-plans")]
    public async Task<IActionResult> GetAllPlans(CancellationToken ct)
    {
        var plans = await _adminService.GetAllPlansAsync(ct);
        return Ok(plans);
    }

    [HttpGet("subscription-plans/{id:guid}")]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken ct)
    {
        var plan = await _adminService.GetPlanByIdAsync(id, ct);
        return Ok(plan);
    }

    [HttpPost("subscription-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanRequest request, CancellationToken ct)
    {
        var plan = await _adminService.CreatePlanAsync(request, ct);
        return Ok(plan);
    }

    [HttpPut("subscription-plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateSubscriptionPlanRequest request, CancellationToken ct)
    {
        var plan = await _adminService.UpdatePlanAsync(id, request, ct);
        return Ok(plan);
    }

    [HttpDelete("subscription-plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        await _adminService.DeletePlanAsync(id, ct);
        return NoContent();
    }

    [HttpPost("subscription-plans/{id:guid}/move")]
    public async Task<IActionResult> MovePlan(Guid id, [FromBody] MoveSubscriptionPlanRequest request, CancellationToken ct)
    {
        await _adminService.MovePlanAsync(id, request.TargetPlanId, ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int? take, [FromQuery] bool staffOnly = false, CancellationToken ct = default)
    {
        var users = await _adminUserService.GetUsersAsync(search, take, staffOnly, ct);
        return Ok(users);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _adminUserService.CreateUserAsync(request, ct);
        return Ok(user);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _adminUserService.GetStatsAsync(ct);
        return Ok(stats);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("users/{id:guid}/moderator-role")]
    public async Task<IActionResult> SetModeratorRole(Guid id, [FromBody] UpdateModeratorRoleRequest request, CancellationToken ct)
    {
        var actorId = GetActorUserId();
        var user = await _adminUserService.SetModeratorRoleAsync(actorId, id, request.IsModerator, ct);
        return Ok(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("users/{id:guid}/transfer-admin")]
    public async Task<IActionResult> TransferAdminRole(Guid id, CancellationToken ct)
    {
        var actorId = GetActorUserId();
        var user = await _adminUserService.TransferAdminRoleAsync(actorId, id, ct);
        return Ok(user);
    }

    private Guid GetActorUserId()
    {
        var claim = User.FindFirst("userId")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var actorId))
            throw new UnauthorizedAccessException("User is not authenticated.");
        return actorId;
    }
}
