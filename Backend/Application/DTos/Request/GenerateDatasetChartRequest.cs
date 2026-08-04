namespace Application.DTos.Request;

public sealed record GenerateDatasetChartRequest(
    string? Prompt,
    string? PrefabChartType,
    string Mode
);