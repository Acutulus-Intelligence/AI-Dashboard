using Domain.Enums;

namespace Domain.Models;

public class DashboardWidget
{
    public Guid Id { get; set; }
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    public WidgetType WidgetType { get; set; } = WidgetType.Chart;
    public Guid? SavedChartId { get; set; }
    public SavedChart? SavedChart { get; set; }
    public string? TextContent { get; set; }
    public TextVariant? TextVariant { get; set; }
    public TextHorizontalAlignment? TextHorizontalAlign { get; set; }
    public TextVerticalAlignment? TextVerticalAlign { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
