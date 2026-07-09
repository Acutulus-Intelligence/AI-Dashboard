using Application.Dtos.Response;
using Domain.Enums;

namespace Application.Interfaces;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanResponse>> GetPlansAsync(UserType? userType = null, CancellationToken ct = default);
    Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task<CheckoutResponse> CreateUserCheckoutSessionAsync(Guid userId, Guid planId, BillingPeriod period, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task<CheckoutResponse> CreateCompanyCheckoutSessionAsync(Guid companyId, Guid planId, BillingPeriod period, Guid actorId, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task<CheckoutResponse> UpgradeToCompanyAsync(Guid userId, string companyName, Guid planId, BillingPeriod period, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task HandleStripeWebhookAsync(string body, string signature, CancellationToken ct = default);
    Task<bool> HasActiveSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CompanyHasActiveSubscriptionAsync(Guid companyId, CancellationToken ct = default);
    Task CancelUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task CancelCompanySubscriptionAsync(Guid companyId, Guid actorId, CancellationToken ct = default);
    Task<UserSubscriptionResponse?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<CompanySubscriptionResponse?> GetCurrentCompanySubscriptionAsync(Guid companyId, CancellationToken ct = default);
}
