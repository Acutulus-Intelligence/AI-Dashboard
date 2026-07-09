using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/charts")]
[Authorize]
public class ChartController : ControllerBase
{
    private readonly IChartService _chartService;

    public ChartController(IChartService chartService)
    {
        _chartService = chartService;
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveChartRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _chartService.SaveChartAsync(userId, request, ct);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var charts = await _chartService.GetChartsAsync(userId, ct);
        return Ok(charts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var chart = await _chartService.GetChartAsync(id, userId, ct);
        return Ok(chart);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _chartService.DeleteChartAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _chartService.ExecuteChartAsync(id, userId, ct);
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
