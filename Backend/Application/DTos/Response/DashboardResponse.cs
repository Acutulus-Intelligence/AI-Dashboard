using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.DTos.Response;

public sealed record DashboardWidgetItem(
    Guid Id,
    WidgetType WidgetType,
    Guid? SavedChartId,
    string? TextContent,
    [property: JsonPropertyName("textVariant")] TextVariant? TextStyle,
    [property: JsonPropertyName("textHorizontalAlign")] TextHorizontalAlignment? HorizontalAlign,
    [property: JsonPropertyName("textVerticalAlign")] TextVerticalAlignment? VerticalAlign,
    string? ChartTitle,
    string? ChartType,
    int PositionX,
    int PositionY,
    int Width,
    int Height
);

public sealed record DashboardResponse(
    Guid Id,
    string Name,
    List<DashboardWidgetItem> Widgets
);
