using Domain.Enums;

namespace Application.DTos.Response;

public sealed record ConnectionResponse(
    Guid Id,
    string Name,
    DbProvider DbProvider,
    bool IsVerified,
    DateTime CreatedAt,
    Guid CreatedById,
    ConnectionVisibility Visibility,
    List<Guid> AllowedRoleIds,
    Guid? CompanyId,
    string Host,
    string Database
);
