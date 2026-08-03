using Domain.Enums;

namespace Application.DTos.Request;

public sealed record CreateConnectionRequest(
    string Name,
    DbProvider DbProvider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    ConnectionVisibility Visibility = ConnectionVisibility.Company,
    List<Guid>? AllowedRoleIds = null,
    SslMode SslMode = SslMode.Prefer
);