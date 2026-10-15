namespace Application.DTos.Request;

public sealed record DisableTwoFactorRequest(
    string Password,
    string? Code = null,
    string? RecoveryCode = null);
