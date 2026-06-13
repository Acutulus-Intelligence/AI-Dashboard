namespace Application.Dtos.Request;

public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string Email);
