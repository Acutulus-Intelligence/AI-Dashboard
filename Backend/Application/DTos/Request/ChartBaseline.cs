using Domain.Charts;

namespace Application.DTos.Request;

/// <summary>
/// Chart metadata sent as refine context. Never includes query result rows.
/// </summary>
public sealed record ChartBaseline(
    string Title,
    string ChartType,
    string XAxis,
    List<string> YAxis,
    string Aggregation,
    string? GroupBy,
    string SqlQuery,
    ChartStyleConfig? StyleConfig = null
);
