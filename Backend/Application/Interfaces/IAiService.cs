using Domain.Models;

namespace Application.Interfaces;

public interface IAiService
{
    Task<AiChartConfig> GenerateChartConfigAsync(string schemaJson, string prompt, string? prefabChartType = null, CancellationToken ct = default);
}
