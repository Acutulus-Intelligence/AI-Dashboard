using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize]
public class ConnectionController : ControllerBase
{
    private readonly IExternalConnectionService _connectionService;

    public ConnectionController(IExternalConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _connectionService.CreateAsync(userId, request, ct);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var connections = await _connectionService.GetAllAsync(userId, ct);
        return Ok(connections);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var connection = await _connectionService.GetByIdAsync(id, userId, ct);
        return Ok(connection);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _connectionService.DeleteAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var success = await _connectionService.TestConnectionAsync(id, userId, ct);
        return Ok(new { isVerified = success });
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}
