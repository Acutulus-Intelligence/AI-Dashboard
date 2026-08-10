using Domain.Enums;

namespace Application.DTos.Response;

public sealed record ConnectionConfigResponse(
    string Name,
    DbProvider DbProvider,
    string ConnectionString,
    ConnectionVisibility Visibility,
    List<Guid> AllowedRoleIds
);