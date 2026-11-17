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

    // The token is carried in the request body (not the URL path) so it is never
    // written to proxy/access logs, browser history, or the Referer header (S3).
    [HttpPost("lookup")]
    public async Task<IActionResult> LookupInvitation([FromBody] LookupInvitationRequest request, CancellationToken ct)
    {
        var invitation = await _companyService.ValidateInvitationAsync(request.Token, ct);
        if (invitation is null)
            return NotFound(new { errorCode = "invitation_invalid", detail = "This invitation is invalid or has expired." });

        return Ok(invitation);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterWithInvitationAsync(
            request.Token,
            new AcceptEmailInvitationRequest(request.FirstName, request.LastName, request.Password),
            ct);
        HttpContext.SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresIn);
        return Ok(new AuthResponse(result.ExpiresIn));
    }
}
