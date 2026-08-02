using Domain.Charts;

namespace Application.DTos.Response;

/// <summary>
/// What the model returned / how we adjusted it — surfaced so generate/refine is debuggable in the UI.
/// </summary>
public sealed record AiGenerationDebug(
    string? RawJson,
    string ChartType,
    string SqlQuery,
    ChartStyleConfig? StyleConfig,
    string? FinishReason,
    IReadOnlyList<string>? Notes
);
