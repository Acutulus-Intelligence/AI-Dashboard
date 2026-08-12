using Domain.Enums;

namespace Application.DTos.Response;

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    UserType UserType,
    bool IsAdmin,
    IReadOnlyList<string> Roles);
