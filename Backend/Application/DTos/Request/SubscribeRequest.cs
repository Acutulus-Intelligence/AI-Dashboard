using Domain.Enums;
namespace Application.Dtos.Request; 
public sealed record SubscribeRequest(
    Guid PlanId, 
    BillingPeriod BillingPeriod);
