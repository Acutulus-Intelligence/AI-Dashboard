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
        CancellationToken ct = default);
}
