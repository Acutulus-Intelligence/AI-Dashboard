namespace Application.DTos.Response;

public sealed record CompanySubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    decimal? NextPrice,
    DateTime? NextPriceEffectiveDate,
    Domain.Enums.BillingPeriod BillingPeriod,
    int? MaxUsers,
    DateTime StartDate,
    DateTime? EndDate,
    Domain.Enums.SubscriptionStatus Status,
    bool CancelAtPeriodEnd,
    DateTime? TrialEndDate);
