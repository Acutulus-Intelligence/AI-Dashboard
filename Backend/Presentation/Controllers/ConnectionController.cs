using Application.DTos.Request;
using Application.Interfaces;
using Domain.Enums;
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
    private readonly IApplicationDbContext _db;

    public ConnectionController(IExternalConnectionService connectionService, IApplicationDbContext db)
    {
        _connectionService = connectionService;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        await EnsureCanManageConnectionsAsync(userId, ct);
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
        await EnsureCanManageConnectionsAsync(userId, ct);
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

    private async Task EnsureCanManageConnectionsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return;
        if (user.UserType != UserType.Company) return;
        var company = await _db.Companies.FindAsync([user.CompanyId], ct);
        if (company is null) return;
        if (user.Id != company.OwnerId)
            throw new UnauthorizedAccessException("Only the company owner can manage connections.");
    }
}
