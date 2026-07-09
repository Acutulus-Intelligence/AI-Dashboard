using Domain.Enums;

namespace Domain.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UserType UserType { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxDashboards { get; set; }
    public int? MaxAiQueriesPerMonth { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserSubscription> UserSubscriptions { get; set; } = [];
    public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = [];
}
