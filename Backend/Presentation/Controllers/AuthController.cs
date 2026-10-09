using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var outcome = await _authService.LoginAsync(request, ct);

        if (outcome.RequiresTwoFactor)
            return Ok(new AuthResponse(0, true, outcome.ChallengeToken));

        var auth = outcome.Auth!;
        HttpContext.SetAuthCookies(auth.AccessToken, auth.RefreshToken, auth.ExpiresIn);
        return Ok(new AuthResponse(auth.ExpiresIn));
    }

    [HttpPost("login/2fa")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginTwoFactorAsync(request, ct);
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
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var me = await _authService.GetMeAsync(userId.Value, ct);
        return Ok(me);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        await _authService.ChangePasswordAsync(userId.Value, request, ct);
        return NoContent();
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        await _authService.UpdateProfileAsync(userId.Value, request, ct);
        return NoContent();
    }

    [HttpDelete("account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        await _authService.DeleteAccountAsync(userId.Value, request, ct);
        return NoContent();
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> SetupTwoFactor([FromBody] SetupTwoFactorRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _authService.SetupTwoFactorAsync(userId.Value, request, ct);
        return Ok(result);
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        // Enabling two-factor authentication revokes all sessions, so the caller
        // must sign in again with their newly configured 2FA.
        var result = await _authService.EnableTwoFactorAsync(userId.Value, request, ct);
        return Ok(result);
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        await _authService.DisableTwoFactorAsync(userId.Value, request, ct);
        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] RegenerateRecoveryCodesRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _authService.RegenerateRecoveryCodesAsync(userId.Value, request, ct);
        return Ok(result);
    }

    private Guid? GetUserId()
        => Guid.TryParse(User.FindFirst("userId")?.Value, out var userId) ? userId : null;
}
