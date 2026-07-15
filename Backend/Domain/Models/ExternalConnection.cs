using Domain.Enums;

namespace Domain.Models;

public class ExternalConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DbProvider DbProvider { get; set; }
    public string EncryptedConnectionString { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
