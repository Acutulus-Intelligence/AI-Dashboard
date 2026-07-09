namespace Application.DTos.Response;

public sealed record ChartResponse(
    Guid Id,
    string Title,
    string ChartType,
    DateTime CreatedAt
);
