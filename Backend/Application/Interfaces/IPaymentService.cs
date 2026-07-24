using Application.Dtos.Response;
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
        BillingPeriod billingPeriod,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    Task<PaymentWebhookEvent?> RetrieveCheckoutSessionAsync(string sessionId, CancellationToken ct = default);

    Task<PaymentWebhookEvent> HandleWebhookAsync(string body, string signature);

    Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task<string> GetOrCreateCustomerAsync(string email, Guid userId, CancellationToken ct = default);
}
