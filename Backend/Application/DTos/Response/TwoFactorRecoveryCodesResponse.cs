namespace Application.DTos.Response;

public sealed record TwoFactorRecoveryCodesResponse(
    IReadOnlyList<string> RecoveryCodes);
