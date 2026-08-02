namespace Domain.Models;

/// <summary>
/// AI chart payload plus optional raw model text for debugging.
/// </summary>
public sealed class AiChartResult
{
    public required AiChartConfig Config { get; init; }

    /// <summary>Exact JSON object extracted from the model reply (before merge).</summary>
    public string? RawJson { get; init; }

    public string? FinishReason { get; init; }
}
