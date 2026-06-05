using Domain.Enums;

namespace Application.DTos.Request;

public sealed record CompanySubscribeRequest(
    Guid PlanId,
    BillingPeriod BillingPeriod
);
