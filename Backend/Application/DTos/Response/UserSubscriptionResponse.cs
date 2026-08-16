namespace Application.DTos.Response;

public sealed record UserSubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    decimal? NextPrice,
    DateTime? NextPriceEffectiveDate,
    Domain.Enums.BillingPeriod BillingPeriod,
    DateTime StartDate,
    DateTime? EndDate,
    Domain.Enums.SubscriptionStatus Status,
    DateTime? TrialEndDate);
