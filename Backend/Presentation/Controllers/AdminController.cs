using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
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
}
