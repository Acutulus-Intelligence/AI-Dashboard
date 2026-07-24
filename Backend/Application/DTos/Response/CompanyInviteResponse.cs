namespace Application.Dtos.Response;

public sealed record CompanyInviteResponse(
    Guid Id,
    string Email,
    string? RoleName,
    Guid RoleId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsExpired,
    bool IsAccepted,
    string CompanyName);
