namespace Application.Common;

/// <summary>
/// Timing rules shared by the subscription read paths and the reconciliation sweep.
/// </summary>
public static class SubscriptionTiming
{
    /// <summary>
    /// Access is kept for a short while after the local period end so a renewal webhook that is
    /// a few seconds or minutes late cannot lock out a paying customer. Checkout guards do not
    /// use this window, so a second subscription can never be started inside it.
    /// </summary>
    public static readonly TimeSpan RenewalGrace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long after expiry the sweep may still verify an already expired row against Stripe.
    /// This lets a row that was expired because of a race repair itself.
    /// </summary>
    public static readonly TimeSpan ExpiryRecoveryWindow = TimeSpan.FromHours(24);

    /// <summary>How long processed Stripe event ids are retained.</summary>
    public static readonly TimeSpan ProcessedEventRetention = TimeSpan.FromDays(30);
}
