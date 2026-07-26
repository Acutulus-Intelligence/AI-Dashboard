namespace Domain.Models;

public class CardFingerprint
{
    public Guid Id { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string StripePaymentMethodId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
