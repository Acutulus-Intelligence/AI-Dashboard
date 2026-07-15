namespace Domain.Models;

public class Dashboard
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "My Dashboard";
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public List<string> AllowedRoles { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
    public ICollection<DashboardWidget> Widgets { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
