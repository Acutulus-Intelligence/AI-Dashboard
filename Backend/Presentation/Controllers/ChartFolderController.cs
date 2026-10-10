using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/chart-folders")]
[Authorize]
[RequireActiveSubscription]
public class ChartFolderController : ControllerBase
{
    private readonly IChartFolderService _folderService;

    public ChartFolderController(IChartFolderService folderService)
    {
        _folderService = folderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var folders = await _folderService.GetFoldersAsync(userId, ct);
        return Ok(folders);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChartFolderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var folder = await _folderService.CreateAsync(userId, request, ct);
        return Ok(folder);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] UpdateChartFolderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var folder = await _folderService.RenameAsync(id, userId, request, ct);
        return Ok(folder);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _folderService.DeleteAsync(id, userId, ct);
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
