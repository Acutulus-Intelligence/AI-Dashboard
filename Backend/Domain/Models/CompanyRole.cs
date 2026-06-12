namespace Domain.Models;

public class CompanyRole
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }

    public bool CanViewAllDashboards { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanManageRoles { get; set; }
    public bool CanManageDashboards { get; set; }

    public List<string> AllowedTables { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}
