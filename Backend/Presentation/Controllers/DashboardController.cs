using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
[RequireActiveSubscription]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboards = await _dashboardService.GetDashboardsAsync(userId, ct);
        return Ok(dashboards);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDashboardRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.CreateDashboardAsync(userId, request, ct);
        return Ok(dashboard);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.GetDashboardAsync(userId, id, ct);
        return Ok(dashboard);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameDashboardRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.RenameDashboardAsync(userId, id, request, ct);
        return Ok(dashboard);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _dashboardService.DeleteDashboardAsync(userId, id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/widgets")]
    public async Task<IActionResult> SaveWidgets(Guid id, [FromBody] SaveWidgetsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.SaveWidgetsAsync(userId, id, request, ct);
        return Ok(dashboard);
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferDashboardRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _dashboardService.TransferOwnershipAsync(userId, id, request, ct);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}
