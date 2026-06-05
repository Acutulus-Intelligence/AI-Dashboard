namespace Application.DTos.Response;

public sealed record CompanyUserResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? RoleName,
    Guid? RoleId,
    bool IsOwner
);
