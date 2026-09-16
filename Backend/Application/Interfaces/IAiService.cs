using Domain.Enums;
using Domain.Models;

namespace Application.Interfaces;

public interface IAiService
{
    Task<AiChartResult> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        string? currentChartJson = null,
        IReadOnlyList<string>? allowedColors = null,
        string? repairHint = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a chart config for an uploaded data collection. Instead of SQL
    /// the response carries a structured <see cref="DataQueryModel"/> evaluated
    /// in-memory (no external database involved).
    /// </summary>
    Task<AiChartConfig> GenerateCollectionChartConfigAsync(
        string schemaJson,
        string prompt,
        string? prefabChartType = null,
        string? repairHint = null,
        CancellationToken ct = default);
}
