using Application.DTos.Response;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPaymentService
{
    Task<CheckoutResponse> CreateCheckoutSessionAsync(
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
        CancellationToken ct = default);

    Task<CheckoutResponse> CreateCompanyCheckoutSessionAsync(
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
        CancellationToken ct = default);

    Task<PaymentWebhookEvent?> RetrieveCheckoutSessionAsync(string sessionId, CancellationToken ct = default);

    Task<PaymentWebhookEvent> HandleWebhookAsync(string body, string signature);

    Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task CancelSubscriptionWithProrationAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task ReactivateSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task SwitchSubscriptionPriceAsync(
        string stripeSubscriptionId,
        string priceId,
        string prorationBehavior = "create_prorations",
        CancellationToken ct = default);

    Task<string> GetOrCreateCustomerAsync(string email, Guid userId, CancellationToken ct = default);

    Task<string> EnsureCustomerExistsAsync(string customerId, string email, Guid userId, CancellationToken ct = default);

    Task<string?> GetPaymentMethodFingerprintAsync(string paymentMethodId, CancellationToken ct = default);

    Task<string?> GetPaymentMethodFingerprintBySubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task EndTrialImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task UpdateSubscriptionTrialEndAsync(string stripeSubscriptionId, DateTime trialEnd, CancellationToken ct = default);

    Task<string> CreateProductAsync(string name, string planId, CancellationToken ct = default);

    Task<string> CreatePriceAsync(string productId, decimal amount, BillingPeriod billingPeriod, CancellationToken ct = default);

    Task UpdateProductAsync(string productId, string name, CancellationToken ct = default);

    Task DeactivateProductAsync(string productId, CancellationToken ct = default);

    Task ActivateProductAsync(string productId, CancellationToken ct = default);

    Task DeactivatePriceAsync(string priceId, CancellationToken ct = default);
}
