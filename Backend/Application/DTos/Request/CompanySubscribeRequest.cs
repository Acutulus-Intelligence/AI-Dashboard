using Domain.Enums;
namespace Application.Dtos.Request; 
public sealed record CompanySubscribeRequest(
    Guid PlanId, 
    BillingPeriod BillingPeriod);
