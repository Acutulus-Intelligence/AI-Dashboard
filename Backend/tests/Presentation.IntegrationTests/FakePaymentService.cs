using System.Collections.Concurrent;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;

namespace Presentation.IntegrationTests;

public sealed class FakePaymentService : IPaymentService
{
    private readonly ConcurrentDictionary<string, PaymentWebhookEvent> _sessions = new();
    private readonly ConcurrentDictionary<string, PaymentWebhookEvent> _subscriptionEvents = new();
    private readonly ConcurrentDictionary<string, Guid> _subscriptionOwners = new();

    public List<int> RecordedTrialDays { get; } = [];

    public List<string> ProratedCancels { get; } = [];

    public List<string> ReactivateFailures { get; } = [];

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
        RecordedTrialDays.Add(trialDays);
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        var subId = $"sub_test_{Guid.NewGuid():N}";
        _subscriptionOwners[subId] = userId;
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
        RecordedTrialDays.Add(trialDays);
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        var subId = $"sub_test_{Guid.NewGuid():N}";
        _subscriptionOwners[subId] = userId;
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

        if (_subscriptionEvents.TryGetValue(body.Trim(), out var subscriptionEvent))
            return Task.FromResult(subscriptionEvent);

        throw new InvalidOperationException("Unknown webhook session body.");
    }

    public void QueueInvoicePaid(string stripeSubscriptionId)
    {
        var key = $"invoice-paid:{stripeSubscriptionId}";
        _subscriptionEvents[key] = new PaymentWebhookEvent(
            "invoice.paid",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            "cus_test");
    }

    public void QueueSubscriptionUpdated(string stripeSubscriptionId, string status)
    {
        var key = $"subscription-updated:{stripeSubscriptionId}";
        _subscriptionEvents[key] = new PaymentWebhookEvent(
            "customer.subscription.updated",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            "cus_test",
            null,
            status);
    }

    public void QueueSubscriptionDeleted(string stripeSubscriptionId)
    {
        var key = $"subscription-deleted:{stripeSubscriptionId}";
        _subscriptionEvents[key] = new PaymentWebhookEvent(
            "customer.subscription.deleted",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            null);
    }

    public List<(string StripeSubscriptionId, string PriceId, string ProrationBehavior)> PriceSwitches { get; } = [];

    public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CancelSubscriptionWithProrationAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        ProratedCancels.Add(stripeSubscriptionId);
        return Task.CompletedTask;
    }

    public Task ReactivateSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        if (ReactivateFailures.Contains(stripeSubscriptionId))
            throw new InvalidOperationException("This subscription has already ended. Subscribe again to continue.");

        return Task.CompletedTask;
    }

    public Task SwitchSubscriptionPriceAsync(
        string stripeSubscriptionId,
        string priceId,
        string prorationBehavior = "create_prorations",
        CancellationToken ct = default)
    {
        PriceSwitches.Add((stripeSubscriptionId, priceId, prorationBehavior));
        return Task.CompletedTask;
    }

    public Task<string> GetOrCreateCustomerAsync(string email, Guid userId, CancellationToken ct = default)
        => Task.FromResult($"cus_test_{userId:N}");

    public Task<string> EnsureCustomerExistsAsync(string customerId, string email, Guid userId, CancellationToken ct = default)
        => Task.FromResult(customerId);

    public Task<string?> GetPaymentMethodFingerprintAsync(string paymentMethodId, CancellationToken ct = default)
        => Task.FromResult<string?>("fp_test");

    public Task<string?> GetPaymentMethodFingerprintBySubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => Task.FromResult<string?>(
            _subscriptionOwners.TryGetValue(stripeSubscriptionId, out var owner)
                ? $"fp_{owner:N}"
                : null);

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
