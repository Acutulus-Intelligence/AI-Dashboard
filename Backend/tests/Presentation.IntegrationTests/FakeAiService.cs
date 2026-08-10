using Application.Interfaces;
using Domain.Enums;
using Domain.Models;

namespace Presentation.IntegrationTests;

public sealed class FakeAiService : IAiService
{
    public Task<AiChartConfig> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        CancellationToken ct = default)
    {
        var chartType = string.IsNullOrWhiteSpace(prefabChartType) ? "bar" : prefabChartType;
        return Task.FromResult(new AiChartConfig
        {
            ChartType = chartType,
            Title = "Sales by category",
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
        });
    }

    public Task<AiChartConfig> GenerateCollectionChartConfigAsync(
        string schemaJson,
        string prompt,
        string? prefabChartType = null,
        CancellationToken ct = default)
    {
        var chartType = string.IsNullOrWhiteSpace(prefabChartType) ? "bar" : prefabChartType;
        return Task.FromResult(new AiChartConfig
        {
            ChartType = chartType,
            Title = "Sales by category",
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = string.Empty,
            DataModel = new DataQueryModel
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
