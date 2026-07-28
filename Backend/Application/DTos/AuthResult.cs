namespace Application.DTos;

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
