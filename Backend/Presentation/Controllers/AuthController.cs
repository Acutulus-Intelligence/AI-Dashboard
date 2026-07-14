using Application.Dtos.Request;
using Application.Dtos.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.CookieExtensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        HttpContext.SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresIn);
        return Ok(new AuthResponse(result.ExpiresIn));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        HttpContext.SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresIn);
        return Ok(new AuthResponse(result.ExpiresIn));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refresh_token"]
            ?? throw new UnauthorizedAccessException("No refresh token found.");

        var result = await _authService.RefreshTokenAsync(refreshToken, ct);
        HttpContext.SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresIn);
        return Ok(new AuthResponse(result.ExpiresIn));
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refresh_token"];
        if (refreshToken is not null)
        {
            await _authService.RevokeTokenAsync(refreshToken, ct);
        }

        HttpContext.RemoveAuthCookies();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsedUserId))
            return Unauthorized();

        var me = await _authService.GetMeAsync(parsedUserId, ct);
        return Ok(me);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null)
            return Unauthorized();

        await _authService.ChangePasswordAsync(Guid.Parse(userId), request, ct);
        return NoContent();
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null)
            return Unauthorized();

        await _authService.UpdateProfileAsync(Guid.Parse(userId), request, ct);
        return NoContent();
    }
}
