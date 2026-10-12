namespace Application.DTos.Request;

public sealed record ReactivateAccountRequest(string CurrentPassword, string? Code = null, string? RecoveryCode = null);
