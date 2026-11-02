namespace Application.Common.Exceptions;

/// <summary>
/// Thrown when an action requires an active (or trialing) subscription that the
/// caller or company does not have. Mapped to HTTP 403.
/// </summary>
public class SubscriptionRequiredException : Exception
{
    public SubscriptionRequiredException(string message) : base(message) { }
}
