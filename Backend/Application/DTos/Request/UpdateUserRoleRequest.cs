namespace Application.DTos.Request;

public sealed record UpdateUserRoleRequest(
    Guid UserId,
    Guid RoleId
);
