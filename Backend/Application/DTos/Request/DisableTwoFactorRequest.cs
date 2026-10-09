namespace Application.DTos.Request;

public sealed record DisableTwoFactorRequest(
    string Code,
    string Password);
