namespace Application.Dtos;

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
