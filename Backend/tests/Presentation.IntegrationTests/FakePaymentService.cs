using System.Collections.Concurrent;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;

namespace Presentation.IntegrationTests;

public sealed class FakePaymentService : IPaymentService
{
    private readonly ConcurrentDictionary<string, PaymentWebhookEvent> _sessions = new();

    public Task<CheckoutResponse> CreateCheckoutSessionAsync(
        string customerId,
        Guid userId,
        Guid planId,
        string planName,
        decimal price,
        string? priceId,
        BillingPeriod billingPeriod,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        var subId = $"sub_test_{Guid.NewGuid():N}";
        _sessions[sessionId] = new PaymentWebhookEvent(
            "checkout.session.completed",
            new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["planId"] = planId.ToString(),
                ["billingPeriod"] = billingPeriod.ToString(),
                ["trialDays"] = trialDays.ToString(),
                ["isCompany"] = "false",
            },
            subId,
            customerId);

        return Task.FromResult(new CheckoutResponse("https://checkout.test/" + sessionId, sessionId));
    }

    public Task<CheckoutResponse> CreateCompanyCheckoutSessionAsync(
        string customerId,
        Guid userId,
        Guid companyId,
        Guid planId,
        string planName,
        decimal price,
        string? priceId,
        BillingPeriod billingPeriod,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        var subId = $"sub_test_{Guid.NewGuid():N}";
        _sessions[sessionId] = new PaymentWebhookEvent(
            "checkout.session.completed",
            new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["companyId"] = companyId.ToString(),
                ["planId"] = planId.ToString(),
                ["billingPeriod"] = billingPeriod.ToString(),
                ["trialDays"] = trialDays.ToString(),
                ["isCompany"] = "true",
            },
            subId,
            customerId);

        return Task.FromResult(new CheckoutResponse("https://checkout.test/" + sessionId, sessionId));
    }

    public Task<PaymentWebhookEvent?> RetrieveCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var evt);
        return Task.FromResult(evt);
    }

    public Task<PaymentWebhookEvent> HandleWebhookAsync(string body, string signature)
    {
        if (!string.Equals(signature, "valid-test-signature", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid Stripe webhook signature.");

        if (_sessions.TryGetValue(body.Trim(), out var evt))
            return Task.FromResult(evt);

        throw new InvalidOperationException("Unknown webhook session body.");
    }

    public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SwitchSubscriptionPriceAsync(string stripeSubscriptionId, string priceId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<string> GetOrCreateCustomerAsync(string email, Guid userId, CancellationToken ct = default)
        => Task.FromResult($"cus_test_{userId:N}");

    public Task<string> EnsureCustomerExistsAsync(string customerId, string email, Guid userId, CancellationToken ct = default)
        => Task.FromResult(customerId);

    public Task<string?> GetPaymentMethodFingerprintAsync(string paymentMethodId, CancellationToken ct = default)
        => Task.FromResult<string?>("fp_test");

    public Task<string?> GetPaymentMethodFingerprintBySubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.FromResult<string?>("fp_test");

    public Task EndTrialImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UpdateSubscriptionTrialEndAsync(
        string stripeSubscriptionId,
        DateTime trialEnd,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<string> CreateProductAsync(string name, string planId, CancellationToken ct = default)
        => Task.FromResult($"prod_test_{planId:N}");

    public Task<string> CreatePriceAsync(string productId, decimal amount, BillingPeriod billingPeriod, CancellationToken ct = default)
        => Task.FromResult($"price_test_{productId}_{billingPeriod}");

    public Task UpdateProductAsync(string productId, string name, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeactivateProductAsync(string productId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ActivateProductAsync(string productId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeactivatePriceAsync(string priceId, CancellationToken ct = default)
        => Task.CompletedTask;
}
