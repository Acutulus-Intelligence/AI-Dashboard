namespace Application.DTos.Response;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);
