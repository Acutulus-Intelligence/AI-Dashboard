namespace Domain.Models;

/// <summary>
/// Durable record of processed Stripe webhook events. Stripe retries deliveries, and the same
/// event can be pushed to several app instances, so handlers must be able to recognise events
/// that were already applied.
/// </summary>
public class ProcessedStripeEvent
{
    /// <summary>Stripe event id (for example <c>evt_...</c>) — the primary key.</summary>
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
