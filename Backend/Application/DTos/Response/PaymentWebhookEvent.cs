namespace Application.DTos.Response;

public sealed record PaymentWebhookEvent(
    string Type,
    IReadOnlyDictionary<string, string> Metadata,
    string? StripeSubscriptionId,
    string? StripeCustomerId,
    string? StripePaymentMethodId = null,
    string? StripeStatus = null,
    DateTime? CurrentPeriodEnd = null,
    DateTime? TrialEnd = null,
    bool? CancelAtPeriodEnd = null);
