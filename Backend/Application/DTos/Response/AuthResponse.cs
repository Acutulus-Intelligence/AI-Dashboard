namespace Application.DTos.Response;

public sealed record AuthResponse(
    int ExpiresIn,
    bool RequiresTwoFactor = false,
    string? ChallengeToken = null);
