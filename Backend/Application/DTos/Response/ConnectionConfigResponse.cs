using Domain.Enums;

namespace Application.DTos.Response;

/// <summary>
/// Connection settings for the edit form. The password is never returned: the
/// masked string is for display and <see cref="PasswordSet"/> tells the client
/// whether a stored secret exists.
/// </summary>
public sealed record ConnectionConfigResponse(
    string Name,
    DbProvider DbProvider,
    string Host,
    int Port,
    string Database,
    string Username,
    bool PasswordSet,
    string MaskedConnectionString,
    ConnectionVisibility Visibility,
    List<Guid> AllowedRoleIds
);
