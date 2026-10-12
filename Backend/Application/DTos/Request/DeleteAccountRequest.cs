namespace Application.DTos.Request;

public sealed record DeleteAccountRequest(string CurrentPassword, string? Code = null, string? RecoveryCode = null);
