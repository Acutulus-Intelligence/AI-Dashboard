namespace Application.DTos.Request;

public sealed record UpdateRoleRequest(
    string Name,
    bool CanViewAllDashboards,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDashboards,
    List<string>? AllowedTables = null
);
