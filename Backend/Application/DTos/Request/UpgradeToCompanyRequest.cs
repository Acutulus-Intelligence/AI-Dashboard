using Domain.Enums;

namespace Application.Dtos.Request;

public sealed record UpgradeToCompanyRequest(string CompanyName, Guid PlanId, BillingPeriod BillingPeriod);
