namespace Application.DTos.Response;

/// <summary>
/// Authoritative Stripe state for a subscription, used to repair local rows when a
/// webhook was missed or arrived with an older payload shape.
/// </summary>
public sealed record SubscriptionSnapshot(
    string Status,
    DateTime? CurrentPeriodEnd,
    DateTime? TrialEnd,
    bool CancelAtPeriodEnd);
