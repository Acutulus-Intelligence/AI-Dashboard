using Domain.Enums;

namespace Application.DTos.Response;

public sealed record ParseConnectionStringResponse(
    DbProvider? Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password
);