namespace Application.Dtos.Request;

public sealed record InviteUserRequest(
    string Email,
    Guid RoleId);
