namespace Application.DTos.Response;

public sealed record CompanyRoleResponse(
    Guid Id,
    string Name,
    bool IsSystemRole,
    bool CanViewAllDashboards,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDashboards,
    bool CanManageConnections,
    List<string> AllowedTables,
    int UserCount);
