namespace Application.DTos.Response;

public sealed record DashboardSummaryResponse(
    Guid Id,
    string Name,
    Guid OwnerId,
    int WidgetCount,
    DateTime UpdatedAt
);
