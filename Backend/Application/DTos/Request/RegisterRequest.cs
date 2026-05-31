namespace Application.DTos.Request;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName
);
