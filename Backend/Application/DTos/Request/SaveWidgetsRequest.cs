namespace Application.DTos.Request;

public sealed record WidgetItem(
    Guid SavedChartId,
    int PositionX,
    int PositionY,
    int Width,
    int Height
);

public sealed record SaveWidgetsRequest(
    List<WidgetItem> Widgets
);
