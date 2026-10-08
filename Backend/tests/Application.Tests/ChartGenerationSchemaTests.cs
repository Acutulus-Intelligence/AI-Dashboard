using System.Text.Json;
using Application.Services;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class ChartGenerationSchemaTests
{
    [Fact]
    public void Json_lists_allowed_tables_columns_and_never_invent_rules()
    {
        var schema = new TableSchema
        {
            TableName = "sales",
            Columns =
            [
                new ColumnSchema { ColumnName = "category", DataType = "text", IsNullable = false },
                new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            ],
        };

        var json = ChartGenerationSchema.ToJson(schema, ["var(--chart-1)"]);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("table").GetString().Should().Be("sales");
        root.GetProperty("allowedTables")[0].GetString().Should().Be("sales");

        var columns = root.GetProperty("allowedColumnNames")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        columns.Should().Equal("category", "amount");

        root.GetProperty("allowedColors")[0].GetString().Should().Be("var(--chart-1)");
        root.GetProperty("allowedChartTypes").GetArrayLength().Should().BeGreaterThan(0);

        var rules = root.GetProperty("rules")
            .EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .ToList();
        rules.Should().Contain(r => r.Contains("Never invent", StringComparison.OrdinalIgnoreCase));
        rules.Should().Contain(r => r.Contains("params", StringComparison.OrdinalIgnoreCase));
        rules.Should().Contain(r => r.Contains("latitude", StringComparison.OrdinalIgnoreCase));
        root.GetProperty("allowedChartTypes").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("map");

        var amount = root.GetProperty("columns").EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "amount");
        amount.GetProperty("usageHint").GetString().Should().Be("measure");
        ChartGenerationSchema.UsageHint("double", "latitude").Should().Be("coordinate");
        ChartGenerationSchema.UsageHint("double", "longitude").Should().Be("coordinate");
    }
}
