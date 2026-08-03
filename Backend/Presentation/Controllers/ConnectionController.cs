using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize]
[RequireActiveSubscription]
public class ConnectionController : ControllerBase
{
    private readonly IExternalConnectionService _connectionService;

    public ConnectionController(IExternalConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    [HttpPost("parse")]
    public async Task<IActionResult> ParseConnectionString([FromBody] ParseConnectionStringRequest request, CancellationToken ct)
    {
        var parsed = await _connectionService.ParseConnectionStringAsync(request.ConnectionString, ct);
        return Ok(parsed);
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
        Response.Headers["X-Company-Connection-Count"] =
            (await _connectionService.GetCompanyConnectionCountAsync(userId, ct)).ToString();
        return Ok(connections);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var connection = await _connectionService.GetByIdAsync(id, userId, ct);
        return Ok(connection);
    }

    [HttpGet("{id:guid}/config")]
    public async Task<IActionResult> GetConfig(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var config = await _connectionService.GetConfigAsync(id, userId, ct);
        return Ok(config);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var connection = await _connectionService.UpdateAsync(id, userId, request, ct);
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
