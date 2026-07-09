namespace Domain.Models;

public class SavedChart
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string ChartType { get; set; } = string.Empty;
    public string XAxis { get; set; } = string.Empty;
    public string[] YAxis { get; set; } = [];
    public string Aggregation { get; set; } = "none";
    public string? GroupBy { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public Guid? ConnectionId { get; set; }
    public string? TableName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
