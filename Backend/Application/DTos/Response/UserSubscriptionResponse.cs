using Domain.Enums;

namespace Application.DTos.Response;

public sealed record UserSubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    BillingPeriod BillingPeriod,
    DateTime StartDate,
    DateTime? EndDate,
    SubscriptionStatus Status
);
