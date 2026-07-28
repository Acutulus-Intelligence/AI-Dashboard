namespace Application.DTos.Request;

public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Email);
