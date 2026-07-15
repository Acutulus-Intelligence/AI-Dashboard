namespace Application.DTos.Response;

public sealed record ChartConfigResponse(
    string ChartType,
    string Title,
    string XAxis,
    List<string> YAxis,
    string Aggregation,
    string? GroupBy,
    string SqlQuery,
    List<Dictionary<string, object?>> QueryResult
);
