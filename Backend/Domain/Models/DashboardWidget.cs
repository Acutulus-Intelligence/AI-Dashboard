namespace Domain.Models;

public class DashboardWidget
{
    public Guid Id { get; set; }
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    public Guid SavedChartId { get; set; }
    public SavedChart SavedChart { get; set; } = null!;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
