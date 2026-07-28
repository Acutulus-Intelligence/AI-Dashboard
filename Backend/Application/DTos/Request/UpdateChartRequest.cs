using Domain.Charts;

namespace Application.DTos.Request;

/// <summary>
/// Edits the presentation of an existing chart. The SQL and axes are deliberately
/// not editable here; those come from generation.
/// </summary>
public sealed record UpdateChartRequest(
    string Title,
    string ChartType,
    ChartStyleConfig? StyleConfig
);
