using Application.Services;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class AiChartPromptBuilderTests
{
    [Fact]
    public void System_prompt_includes_schema_allowlists_and_never_invent_rules()
    {
        var schema = ChartGenerationSchema.ToJson(new TableSchema
        {
            TableName = "sales",
            Columns =
            [
                new ColumnSchema { ColumnName = "category", DataType = "text", IsNullable = false },
                new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            ],
        }, ["var(--chart-1)"]);

        var prompt = AiChartPromptBuilder.BuildSystemPrompt(
            schema, DbProvider.PostgreSql, prefabChartType: null, currentChartJson: null, ["var(--chart-1)"]);

        prompt.Should().Contain("ALLOWED TABLES");
        prompt.Should().Contain("ALLOWED COLUMNS");
        prompt.Should().Contain("sales");
        prompt.Should().Contain("amount");
        prompt.Should().Contain("measure");
        prompt.Should().Contain("Never invent");
        prompt.Should().Contain("var(--chart-1)");
        prompt.Should().Contain("params");
        prompt.Should().Contain("\"map\"");
        prompt.Should().Contain("choropleth");
        prompt.Should().NotContain("heatmap");
    }

    [Fact]
    public void System_prompt_canonicalizes_known_prefab_and_ignores_unknown_types()
    {
        var schema = ChartGenerationSchema.ToJson(new TableSchema
        {
            TableName = "sales",
            Columns =
            [
                new ColumnSchema { ColumnName = "country", DataType = "text", IsNullable = false },
                new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            ],
        });

        var mapPrompt = AiChartPromptBuilder.BuildSystemPrompt(
            schema, DbProvider.PostgreSql, prefabChartType: "Map", currentChartJson: null, ["var(--chart-1)"]);
        mapPrompt.Should().Contain("The user prefers the chart type: map.");

        var unknownPrompt = AiChartPromptBuilder.BuildSystemPrompt(
            schema, DbProvider.PostgreSql, prefabChartType: "heatmap", currentChartJson: null, ["var(--chart-1)"]);
        unknownPrompt.Should().NotContain("heatmap");
        unknownPrompt.Should().Contain("Choose the best chart type based on the data.");
    }

    [Fact]
    public void Collection_prompt_includes_color_allowlist()
    {
        var schema = ChartGenerationSchema.ToJson("sales", ["category", "amount"], ["text", "numeric"], ["var(--chart-2)"]);

        var prompt = AiChartPromptBuilder.BuildCollectionSystemPrompt(schema, null, ["var(--chart-2)"]);

        prompt.Should().Contain("ALLOWED COLUMNS");
        prompt.Should().Contain("var(--chart-2)");
        prompt.Should().Contain("dataModel");
        prompt.Should().Contain("params");
        prompt.Should().Contain("choropleth");
        prompt.Should().NotContain("heatmap");
    }

    [Fact]
    public void AppendRepairHint_prefixes_validation_error()
    {
        var result = AiChartPromptBuilder.AppendRepairHint("show sales", "Unknown column(s) in SQL: revenue.");

        result.Should().Contain("Validation error: Unknown column(s) in SQL: revenue.");
        result.Should().Contain("Original request:");
        result.Should().Contain("show sales");
    }
}
