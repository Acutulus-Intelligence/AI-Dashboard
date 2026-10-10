using Domain.Charts;

namespace Application.DTos.Request;

/// <summary>
/// Replaces presentation and optionally the generated query/axes of an existing chart.
/// </summary>
public sealed record UpdateChartRequest(
    string Title,
    string ChartType,
    string XAxis,
    List<string> YAxis,
    string Aggregation,
    string? GroupBy,
    string SqlQuery,
    ChartStyleConfig? StyleConfig = null
);
