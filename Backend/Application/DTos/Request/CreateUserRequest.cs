using Domain.Enums;

namespace Application.DTos.Request;

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserType UserType,
    string Role);