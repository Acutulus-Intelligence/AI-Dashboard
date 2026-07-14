using Domain.Enums;
using Domain.Models;

namespace Application.Interfaces;

public interface IAiService
{
    Task<AiChartConfig> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        CancellationToken ct = default);
}
