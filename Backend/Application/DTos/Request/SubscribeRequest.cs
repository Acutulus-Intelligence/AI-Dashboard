using Domain.Enums;

namespace Application.DTos.Request;

public sealed record SubscribeRequest(
    Guid PlanId,
    BillingPeriod BillingPeriod
);
