namespace Application.DTos.Response;

public sealed record DashboardWidgetItem(
    Guid Id,
    Guid SavedChartId,
    string ChartTitle,
    string ChartType,
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
