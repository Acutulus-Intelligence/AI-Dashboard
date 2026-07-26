using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/graphs")]
[Authorize]
[RequireActiveSubscription]
public class GraphController : ControllerBase
{
    private readonly IGraphGenerationService _graphService;

    public GraphController(IGraphGenerationService graphService)
    {
        _graphService = graphService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateChartRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _graphService.GenerateAsync(request, userId, ct);
        return Ok(response);
    }

    [HttpPost("manual")]
    public async Task<IActionResult> Manual([FromBody] GenerateChartRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _graphService.ManualAsync(request, userId, ct);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}
