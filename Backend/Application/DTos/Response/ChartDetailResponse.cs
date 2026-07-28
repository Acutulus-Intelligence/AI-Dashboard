using Domain.Charts;

namespace Application.DTos.Response;

public sealed record ChartDetailResponse(
    Guid Id,
    string Title,
    string ChartType,
    string XAxis,
    List<string> YAxis,
    string Aggregation,
    string? GroupBy,
    string SqlQuery,
    Guid? ConnectionId,
    string? TableName,
    DateTime CreatedAt,
    ChartStyleConfig? StyleConfig = null
);
