namespace Application.DTos;

public sealed record LoginOutcome(
    AuthResult? Auth,
    string? ChallengeToken)
{
    public bool RequiresTwoFactor => ChallengeToken is not null;

    public static LoginOutcome Authenticated(AuthResult auth) => new(auth, null);

    public static LoginOutcome TwoFactorRequired(string challengeToken) => new(null, challengeToken);
}
