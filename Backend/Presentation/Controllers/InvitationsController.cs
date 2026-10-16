using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.CookieExtensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/invitations")]
[AllowAnonymous]
[EnableRateLimiting("invite-accept")]
public class InvitationsController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IAuthService _authService;

    public InvitationsController(ICompanyService companyService, IAuthService authService)
    {
        _companyService = companyService;
        _authService = authService;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> GetInvitation(string token, CancellationToken ct)
    {
        var invitation = await _companyService.ValidateInvitationAsync(token, ct);
        if (invitation is null)
            return NotFound(new { errorCode = "invitation_invalid", detail = "This invitation is invalid or has expired." });

        return Ok(invitation);
    }

    [HttpPost("{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token, [FromBody] AcceptEmailInvitationRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterWithInvitationAsync(token, request, ct);
        HttpContext.SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresIn);
        return Ok(new AuthResponse(result.ExpiresIn));
    }
}
