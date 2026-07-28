using Domain.Enums;

namespace Application.DTos.Request;

public sealed record UpgradeToCompanyRequest(string CompanyName, Guid PlanId, BillingPeriod BillingPeriod, string SuccessUrl, string CancelUrl);
