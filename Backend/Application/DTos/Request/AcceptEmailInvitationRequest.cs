namespace Application.DTos.Request;

public sealed record AcceptEmailInvitationRequest(
    string FirstName,
    string LastName,
    string Password);
