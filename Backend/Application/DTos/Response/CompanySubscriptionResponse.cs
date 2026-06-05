using Domain.Enums;

namespace Application.DTos.Response;

public sealed record CompanySubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    BillingPeriod BillingPeriod,
    int? MaxUsers,
    DateTime StartDate,
    DateTime? EndDate,
    SubscriptionStatus Status
);
