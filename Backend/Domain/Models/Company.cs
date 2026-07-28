using Domain.Charts;

namespace Domain.Models;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    /// <summary>Company brand preferences (colours today; expandable later).</summary>
    public CompanyStyleConfig? StyleConfig { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<CompanyRole> CompanyRoles { get; set; } = [];
    public ICollection<Dashboard> Dashboards { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
