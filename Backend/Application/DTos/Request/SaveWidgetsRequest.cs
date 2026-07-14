using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.DTos.Request;

public sealed record WidgetItem(
    Guid? Id,
    WidgetType WidgetType,
    Guid? SavedChartId,
    string? TextContent,
    [property: JsonPropertyName("textVariant")] TextVariant? TextStyle,
    [property: JsonPropertyName("textHorizontalAlign")] TextHorizontalAlignment? HorizontalAlign,
    [property: JsonPropertyName("textVerticalAlign")] TextVerticalAlignment? VerticalAlign,
    int PositionX,
    int PositionY,
    int Width,
    int Height
);

public sealed record SaveWidgetsRequest(
    List<WidgetItem> Widgets
);
