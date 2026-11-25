using Domain.Charts;

namespace Application.DTos.Request;

/// <summary>
/// Updates only the presentation of an existing chart, leaving its query and
/// axes untouched. Used by the dashboard quick-style popover.
/// </summary>
public sealed record UpdateChartStyleRequest(ChartStyleConfig? StyleConfig = null);
