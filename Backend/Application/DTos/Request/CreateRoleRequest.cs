namespace Application.Dtos.Request;

public sealed record CreateRoleRequest(
    string Name,
    bool CanViewAllDashboards,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDashboards,
    List<string>? AllowedTables = null);
