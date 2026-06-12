namespace Application.Dtos.Response;

public sealed record CompanySubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    Domain.Enums.BillingPeriod BillingPeriod,
    int? MaxUsers,
    DateTime StartDate,
    DateTime? EndDate,
    Domain.Enums.SubscriptionStatus Status);
