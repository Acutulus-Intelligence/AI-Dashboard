namespace Application.DTos.Request;

public sealed record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);
