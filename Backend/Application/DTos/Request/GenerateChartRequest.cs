namespace Application.DTos.Request;

public sealed record GenerateChartRequest(
    Guid ConnectionId,
    string TableName,
    string? Prompt,
    string? PrefabChartType,
    string Mode,
    ChartBaseline? CurrentChart = null
);
