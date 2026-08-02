using Domain.Enums;

namespace Application.DTos.Response;

public sealed record ConnectionConfigResponse(
    string Name,
    DbProvider DbProvider,
    string Host,
    int Port,
    string Database,
    string Username,
    ConnectionVisibility Visibility,
    List<Guid> AllowedRoleIds
);
