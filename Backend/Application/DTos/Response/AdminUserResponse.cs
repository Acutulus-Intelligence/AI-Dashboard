using Domain.Enums;

namespace Application.DTos.Response;

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    UserType UserType,
    bool IsAdmin,
    bool IsModerator,
    IReadOnlyList<string> Roles);
