namespace Application.DTos.Request;

public sealed record UpdateRoleRequest(
    string Name,
    bool CanViewAllDashboards,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDashboards,
    bool CanManageConnections = false,
    List<string>? AllowedTables = null);
