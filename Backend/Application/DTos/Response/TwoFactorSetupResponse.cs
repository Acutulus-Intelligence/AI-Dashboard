namespace Application.DTos.Response;

public sealed record TwoFactorSetupResponse(
    string SharedKey,
    string AuthenticatorUri);
