namespace Application.DTos.Request;

public sealed record TwoFactorLoginRequest(
    string ChallengeToken,
    string Code,
    bool UseRecoveryCode = false);
