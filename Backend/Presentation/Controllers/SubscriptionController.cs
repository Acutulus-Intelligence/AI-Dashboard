using Application.DTos.Request;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans([FromQuery] UserType? userType, CancellationToken ct)
    {
        var plans = await _subscriptionService.GetPlansAsync(userType, ct);
        return Ok(plans);
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken ct)
    {
        var plan = await _subscriptionService.GetPlanByIdAsync(id, ct);
        return Ok(plan);
    }

    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _subscriptionService.SubscribeUserAsync(userId, request.PlanId, request.BillingPeriod, ct);
        return Ok(response);
    }

    [HttpPost("company/{companyId:guid}/subscribe")]
    [Authorize]
    public async Task<IActionResult> SubscribeCompany(Guid companyId, [FromBody] CompanySubscribeRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        var response = await _subscriptionService.SubscribeCompanyAsync(companyId, request.PlanId, request.BillingPeriod, actorId, ct);
        return Ok(response);
    }

    [HttpGet("current")]
    [Authorize]
    public async Task<IActionResult> GetCurrentSubscription(CancellationToken ct)
    {
        var userId = GetUserId();
        var subscription = await _subscriptionService.GetCurrentUserSubscriptionAsync(userId, ct);
        if (subscription is null)
            return NotFound();
        return Ok(subscription);
    }

    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        var userId = GetUserId();
        await _subscriptionService.CancelUserSubscriptionAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("company/{companyId:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelCompanySubscription(Guid companyId, CancellationToken ct)
    {
        var actorId = GetUserId();
        await _subscriptionService.CancelCompanySubscriptionAsync(companyId, actorId, ct);
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
