using Domain.Enums;

namespace Domain.Models;

public class UserSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal? NextPrice { get; set; }
    public DateTime? NextPriceEffectiveDate { get; set; }
    public BillingPeriod BillingPeriod { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool CancelAtPeriodEnd { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? TrialEndDate { get; set; }
}
