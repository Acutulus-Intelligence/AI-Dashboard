namespace Application.Dtos.Request;

public sealed record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);
