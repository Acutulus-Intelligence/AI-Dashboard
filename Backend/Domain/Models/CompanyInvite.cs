using Domain.Enums;

namespace Domain.Models;

public class CompanyInvite
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public Guid? RoleId { get; set; }
    public CompanyRole? Role { get; set; }

    // Snapshot of the role name at invitation time. Kept so accepted invite
    // history survives deleting the role.
    public string RoleName { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public InviteDeliveryMethod DeliveryMethod { get; set; } = InviteDeliveryMethod.Internal;
    public DateTime? LastSentAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
