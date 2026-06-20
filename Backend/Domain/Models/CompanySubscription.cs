using Domain.Enums;

namespace Domain.Models;

public class CompanySubscription
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public decimal Price { get; set; }
    public BillingPeriod BillingPeriod { get; set; }
    public int? MaxUsers { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string? StripeSubscriptionId { get; set; }
    public DateTime? TrialEndDate { get; set; }
}
