using Application.Interfaces;
using Domain.Enums;
using Domain.Models;

namespace Presentation.IntegrationTests;

public sealed class FakeAiService : IAiService
{
    public Task<AiChartResult> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        string? currentChartJson = null,
        IReadOnlyList<string>? allowedColors = null,
        string? repairHint = null,
        CancellationToken ct = default)
    {
        var chartType = string.IsNullOrWhiteSpace(prefabChartType) ? "bar" : prefabChartType;

        // When refining, echo a slightly adjusted title so tests can assert the path works.
        var title = string.IsNullOrWhiteSpace(currentChartJson)
            ? "Sales by category"
            : "Sales by category (refined)";

        var config = new AiChartConfig
        {
            ChartType = chartType,
            Title = title,
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
        };

        return Task.FromResult(new AiChartResult
        {
            Config = config,
            RawJson = """{"chartType":"bar","title":"Sales by category"}""",
            FinishReason = "stop",
        });
    }

    public Task<AiChartConfig> GenerateCollectionChartConfigAsync(
        string schemaJson,
        string prompt,
        string? prefabChartType = null,
        string? currentChartJson = null,
        IReadOnlyList<string>? allowedColors = null,
        string? repairHint = null,
        CancellationToken ct = default)
    {
        var chartType = string.IsNullOrWhiteSpace(prefabChartType) ? "bar" : prefabChartType;
        var isRefine = !string.IsNullOrWhiteSpace(currentChartJson);

        // On refine, omit the data model so the service must merge the baseline.
        return Task.FromResult(new AiChartConfig
        {
            ChartType = chartType,
            Title = isRefine ? "Sales by category (refined)" : "Sales by category",
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = string.Empty,
            DataModel = isRefine
                ? null
                : new DataQueryModel
                {
                    Filters = [],
                    GroupBy = ["category"],
                    Aggregations = [new DataAggregation { Column = "amount", Function = "sum" }],
                    OrderBy = [new DataOrderBy { Column = "amount", Direction = "desc" }],
                    Limit = 10,
                },
        });
    }
}
