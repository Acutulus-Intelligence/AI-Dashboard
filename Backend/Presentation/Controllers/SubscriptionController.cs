using Application.Dtos.Request;
using Application.Dtos.Response;
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

    [HttpPost("create-checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckout([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var (successUrl, cancelUrl) = GetReturnUrls();
        var response = await _subscriptionService.CreateUserCheckoutSessionAsync(
            userId, request.PlanId, request.BillingPeriod, successUrl, cancelUrl, ct);
        return Ok(response);
    }

    [HttpPost("company/{companyId:guid}/create-checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCompanyCheckout(Guid companyId, [FromBody] SubscribeRequest request, CancellationToken ct)
    {
        var actorId = GetUserId();
        var (successUrl, cancelUrl) = GetReturnUrls();
        var response = await _subscriptionService.CreateCompanyCheckoutSessionAsync(
            companyId, request.PlanId, request.BillingPeriod, actorId, successUrl, cancelUrl, ct);
        return Ok(response);
    }

    [HttpPost("upgrade-to-company")]
    [Authorize]
    public async Task<IActionResult> UpgradeToCompany([FromBody] UpgradeToCompanyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var (successUrl, cancelUrl) = GetReturnUrls();
        var response = await _subscriptionService.UpgradeToCompanyAsync(
            userId, request.CompanyName, request.PlanId, request.BillingPeriod, successUrl, cancelUrl, ct);
        return Ok(response);
    }

    [HttpPost("stripe-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = HttpContext.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? "";
        await _subscriptionService.HandleStripeWebhookAsync(json, signature, ct);
        return Ok();
    }

    [HttpGet("has-active")]
    [Authorize]
    public async Task<IActionResult> HasActive(CancellationToken ct)
    {
        var userId = GetUserId();
        var hasActive = await _subscriptionService.HasActiveSubscriptionAsync(userId, ct);
        return Ok(new HasActiveSubscriptionResponse(hasActive));
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

    [HttpGet("company/{companyId:guid}/current")]
    [Authorize]
    public async Task<IActionResult> GetCurrentCompanySubscription(Guid companyId, CancellationToken ct)
    {
        var subscription = await _subscriptionService.GetCurrentCompanySubscriptionAsync(companyId, ct);
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

    [HttpGet("company/{companyId:guid}/has-active")]
    [Authorize]
    public async Task<IActionResult> CompanyHasActive(Guid companyId, CancellationToken ct)
    {
        var hasActive = await _subscriptionService.CompanyHasActiveSubscriptionAsync(companyId, ct);
        return Ok(new HasActiveSubscriptionResponse(hasActive));
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }

    private (string successUrl, string cancelUrl) GetReturnUrls()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return ($"{baseUrl}/api/subscriptions/checkout/success", $"{baseUrl}/api/subscriptions/checkout/cancel");
    }
}
