using Application.DTos.Response;
using Domain.Enums;

namespace Application.Interfaces;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanResponse>> GetPlansAsync(UserType? userType = null, CancellationToken ct = default);
    Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task<UserSubscriptionResponse> SubscribeUserAsync(Guid userId, Guid planId, BillingPeriod period, CancellationToken ct = default);
    Task<CompanySubscriptionResponse> SubscribeCompanyAsync(Guid companyId, Guid planId, BillingPeriod period, Guid actorId, CancellationToken ct = default);
    Task CancelUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task CancelCompanySubscriptionAsync(Guid companyId, Guid actorId, CancellationToken ct = default);
    Task<UserSubscriptionResponse?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<CompanySubscriptionResponse?> GetCurrentCompanySubscriptionAsync(Guid companyId, CancellationToken ct = default);
}
