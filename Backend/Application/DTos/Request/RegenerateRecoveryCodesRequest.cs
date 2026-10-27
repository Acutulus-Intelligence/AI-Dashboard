namespace Application.DTos.Request;

public sealed record RegenerateRecoveryCodesRequest(
    string Code,
    string Password);
