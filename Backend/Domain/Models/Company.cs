namespace Domain.Models;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Dashboard> Dashboards { get; set; } = [];
}
