namespace Application.DTos.Request;

public sealed record EnableTwoFactorRequest(
    string Code);
