namespace Application.DTos.Request;

public sealed record LoginRequest(
    string Email,
    string Password
);
