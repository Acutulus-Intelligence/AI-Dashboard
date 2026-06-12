namespace Application.Dtos.Request;

public sealed record LoginRequest(
    string Email,
    string Password);
