namespace Application.Dtos.Response;

public sealed record CompanyRoleResponse(
    Guid Id,
    string Name,
    bool IsSystemRole,
    bool CanViewAllDashboards,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDashboards,
    List<string> AllowedTables,
    int UserCount);
