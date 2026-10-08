namespace Domain.Models;

/// <summary>
/// A flat, per-user folder used to organize saved charts.
/// </summary>
public class ChartFolder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
