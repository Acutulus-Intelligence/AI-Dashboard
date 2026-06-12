namespace Application.Dtos.Response;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
