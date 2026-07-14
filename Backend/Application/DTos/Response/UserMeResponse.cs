using Domain.Enums;

namespace Application.Dtos.Response;

public sealed record UserMeResponse(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    UserType UserType,
    string? FirstName,
    string? LastName
);
