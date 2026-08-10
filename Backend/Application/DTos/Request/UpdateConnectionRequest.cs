using Domain.Enums;

namespace Application.DTos.Request;

public sealed record UpdateConnectionRequest(
    string Name,
    DbProvider DbProvider,
    string ConnectionString,
    ConnectionVisibility Visibility = ConnectionVisibility.Company,
    List<Guid>? AllowedRoleIds = null
);