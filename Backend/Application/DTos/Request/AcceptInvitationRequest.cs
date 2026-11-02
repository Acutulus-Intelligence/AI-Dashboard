namespace Application.DTos.Request;

public sealed record AcceptInvitationRequest(
    string Token,
    string FirstName,
    string LastName,
    string Password);
