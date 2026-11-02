namespace Application.DTos.Response;

public sealed record InvitationDetailsResponse(
    Guid InviteId,
    string Email,
    string CompanyName,
    string RoleName,
    DateTime ExpiresAt,
    bool HasAccount);
