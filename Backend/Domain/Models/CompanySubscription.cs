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
    public decimal? NextPrice { get; set; }
    public DateTime? NextPriceEffectiveDate { get; set; }
    public BillingPeriod BillingPeriod { get; set; }
    public int? MaxUsers { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool CancelAtPeriodEnd { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? TrialEndDate { get; set; }

    /// <summary>
    /// Creation timestamp of the newest Stripe event already applied to this row. Used to drop
    /// out-of-order webhook deliveries.
    /// </summary>
    public DateTime? LastStripeEventAt { get; set; }
}
