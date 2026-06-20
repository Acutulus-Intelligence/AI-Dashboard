namespace Application.Dtos.Response;

public sealed record UserSubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    Domain.Enums.BillingPeriod BillingPeriod,
    DateTime StartDate,
    DateTime? EndDate,
    Domain.Enums.SubscriptionStatus Status,
    DateTime? TrialEndDate);
