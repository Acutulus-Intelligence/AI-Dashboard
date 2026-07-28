using Domain.Enums;

namespace Application.DTos.Response;

public sealed record UserMeResponse(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    UserType UserType,
    string? FirstName,
    string? LastName,
    string? CompanyRoleName
);
