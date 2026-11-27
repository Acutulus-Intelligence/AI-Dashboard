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

    private DateTime _eventClock = DateTime.UtcNow.AddHours(-1);

    private DateTime NextEventAt() => _eventClock = _eventClock.AddSeconds(1);

    private static string NewEventId() => $"evt_{Guid.NewGuid():N}";

    public List<int> RecordedTrialDays { get; } = [];

    public List<string> ProratedCancels { get; } = [];

    public List<string> ReactivateFailures { get; } = [];

    /// <summary>Queues an arbitrary event body, for idempotency/ordering tests.</summary>
    public void QueueEvent(string body, PaymentWebhookEvent evt) => _subscriptionEvents[body] = evt;

    public static string InvoicePaidBody(string stripeSubscriptionId) => $"invoice-paid:{stripeSubscriptionId}";

    public static string SubscriptionUpdatedBody(string stripeSubscriptionId) => $"subscription-updated:{stripeSubscriptionId}";

    public static string SubscriptionDeletedBody(string stripeSubscriptionId) => $"subscription-deleted:{stripeSubscriptionId}";

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

    public void QueueInvoicePaid(
        string stripeSubscriptionId,
        DateTime? currentPeriodEnd = null,
        long? amountPaid = null,
        string? billingReason = null,
        string? eventId = null,
        DateTime? eventCreatedAt = null)
    {
        _subscriptionEvents[InvoicePaidBody(stripeSubscriptionId)] = new PaymentWebhookEvent(
            "invoice.paid",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            "cus_test",
            CurrentPeriodEnd: currentPeriodEnd,
            StripeEventId: eventId ?? NewEventId(),
            EventCreatedAt: eventCreatedAt ?? NextEventAt(),
            AmountPaid: amountPaid,
            BillingReason: billingReason);
    }

    /// <summary>
    /// Simulates an invoice.paid event from an older webhook API version, where the payload has
    /// no subscription id at all.
    /// </summary>
    public void QueueInvoicePaidWithoutSubscription(string customerId = "cus_test")
    {
        var key = $"invoice-paid-no-sub:{customerId}";
        _subscriptionEvents[key] = new PaymentWebhookEvent(
            "invoice.paid",
            new Dictionary<string, string>(),
            null,
            customerId,
            StripeEventId: NewEventId(),
            EventCreatedAt: NextEventAt());
    }

    public void QueueSubscriptionUpdated(
        string stripeSubscriptionId,
        string status,
        DateTime? currentPeriodEnd = null,
        bool? cancelAtPeriodEnd = null,
        DateTime? trialEnd = null,
        string? eventId = null,
        DateTime? eventCreatedAt = null)
    {
        _subscriptionEvents[SubscriptionUpdatedBody(stripeSubscriptionId)] = new PaymentWebhookEvent(
            "customer.subscription.updated",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            "cus_test",
            null,
            status,
            currentPeriodEnd,
            trialEnd,
            cancelAtPeriodEnd,
            eventId ?? NewEventId(),
            eventCreatedAt ?? NextEventAt());
    }

    public void QueueSubscriptionDeleted(
        string stripeSubscriptionId,
        string? eventId = null,
        DateTime? eventCreatedAt = null)
    {
        _subscriptionEvents[SubscriptionDeletedBody(stripeSubscriptionId)] = new PaymentWebhookEvent(
            "customer.subscription.deleted",
            new Dictionary<string, string>(),
            stripeSubscriptionId,
            null,
            StripeEventId: eventId ?? NewEventId(),
            EventCreatedAt: eventCreatedAt ?? NextEventAt());
    }

    /// <summary>
    /// A subscription created outside the app (Stripe Dashboard or API) carries no metadata.
    /// </summary>
    public void QueueCheckoutWithoutMetadata(string body = "checkout-no-metadata")
    {
        _subscriptionEvents[body] = new PaymentWebhookEvent(
            "checkout.session.completed",
            new Dictionary<string, string>(),
            $"sub_external_{Guid.NewGuid():N}",
            "cus_external",
            StripeEventId: NewEventId(),
            EventCreatedAt: NextEventAt());
    }

    public List<(string StripeSubscriptionId, string PriceId, string ProrationBehavior)> PriceSwitches { get; } = [];

    public List<string> CancelAtPeriodEndRequests { get; } = [];

    /// <summary>Stripe subscriptions that no longer exist (resource_missing).</summary>
    public HashSet<string> MissingSubscriptions { get; } = [];

    /// <summary>Authoritative Stripe state used by the reconciliation sweep.</summary>
    public ConcurrentDictionary<string, SubscriptionSnapshot> Snapshots { get; } = new();

    public Task<bool> CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        CancelAtPeriodEndRequests.Add(stripeSubscriptionId);
        return Task.FromResult(!MissingSubscriptions.Contains(stripeSubscriptionId));
    }

    public Task<SubscriptionSnapshot?> GetSubscriptionSnapshotAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        Snapshots.TryGetValue(stripeSubscriptionId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public List<string> ImmediateCancelFailures { get; } = [];

    public List<string> ImmediateCancels { get; } = [];

    public Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        if (ImmediateCancelFailures.Contains(stripeSubscriptionId))
            throw new InvalidOperationException("Stripe is unavailable.");

        ImmediateCancels.Add(stripeSubscriptionId);
        return Task.CompletedTask;
    }

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
