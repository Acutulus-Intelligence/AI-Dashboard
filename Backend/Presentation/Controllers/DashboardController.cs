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
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.GetDashboardAsync(userId, ct);
        return Ok(dashboard);
    }

    [HttpPut("widgets")]
    public async Task<IActionResult> SaveWidgets([FromBody] SaveWidgetsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var dashboard = await _dashboardService.SaveWidgetsAsync(userId, request, ct);
        return Ok(dashboard);
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}
