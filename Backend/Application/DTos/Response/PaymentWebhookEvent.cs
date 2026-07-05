namespace Application.Dtos.Response;

public sealed record PaymentWebhookEvent(
    string Type,
    IReadOnlyDictionary<string, string> Metadata,
    string? StripeSubscriptionId,
    string? StripeCustomerId);
