using Application.Services;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using System.Text.Json;

namespace Application.Tests;

public class AiOutputGroundingTests
{
    private static TableSchema SalesSchema() => new()
    {
        TableName = "sales",
        Columns =
        [
            new ColumnSchema { ColumnName = "category", DataType = "text", IsNullable = false },
            new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            new ColumnSchema { ColumnName = "created_at", DataType = "timestamp", IsNullable = false },
        ],
    };

    private static AiChartConfig Chart(string sql, string x = "category", string y = "amount", string? groupBy = "category") =>
        new()
        {
            ChartType = "bar",
            Title = "Sales",
            XAxis = x,
            YAxis = [y],
            Aggregation = "sum",
            GroupBy = groupBy,
            SqlQuery = sql,
        };

    [Fact]
    public void ValidateSqlChart_accepts_grounded_select()
    {
        var config = Chart("SELECT category, SUM(amount) AS amount FROM sales GROUP BY category");

        AiOutputGrounding.ValidateSqlChart(config, SalesSchema()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSqlChart_accepts_quoted_identifiers_and_table_alias()
    {
        var config = Chart("""SELECT s."category", SUM(s."amount") AS amount FROM "sales" AS s GROUP BY s."category" """);

        AiOutputGrounding.ValidateSqlChart(config, SalesSchema()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSqlChart_accepts_implicit_table_alias()
    {
        var config = Chart("SELECT s.category, SUM(s.amount) AS total FROM sales s GROUP BY s.category", y: "total");

        AiOutputGrounding.ValidateSqlChart(config, SalesSchema()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSqlChart_rejects_invented_table()
    {
        var config = Chart(
            "SELECT category, SUM(amount) AS amount FROM customers GROUP BY category");

        var result = AiOutputGrounding.ValidateSqlChart(config, SalesSchema());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("customers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSqlChart_rejects_join_to_unknown_table()
    {
        var config = Chart(
            "SELECT sales.category, customers.name FROM sales JOIN customers ON customers.id = sales.customer_id");

        var result = AiOutputGrounding.ValidateSqlChart(config, SalesSchema());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("customers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSqlChart_rejects_invented_column_in_sql()
    {
        var config = Chart(
            "SELECT customer_name, SUM(amount) AS amount FROM sales GROUP BY customer_name",
            x: "customer_name",
            groupBy: "customer_name");

        var result = AiOutputGrounding.ValidateSqlChart(config, SalesSchema());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("customer_name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSqlChart_rejects_invented_axis_field()
    {
        var config = Chart(
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            x: "region");

        var result = AiOutputGrounding.ValidateSqlChart(config, SalesSchema());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("xAxis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSqlChart_ignores_string_literals_that_look_like_columns()
    {
        var config = Chart(
            "SELECT category, SUM(amount) AS amount FROM sales WHERE category = 'customer_name' GROUP BY category");

        AiOutputGrounding.ValidateSqlChart(config, SalesSchema()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCollectionChart_rejects_invented_data_model_column()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            DataModel = new DataQueryModel
            {
                GroupBy = ["category"],
                Aggregations = [new DataAggregation { Column = "revenue", Function = "sum" }],
            },
        };

        var result = AiOutputGrounding.ValidateCollectionChart(config, ["category", "amount"]);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("revenue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCollectionChart_accepts_schema_columns_case_insensitively()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "Category",
            YAxis = ["Amount"],
            Aggregation = "sum",
            GroupBy = "Category",
            DataModel = new DataQueryModel
            {
                GroupBy = ["CATEGORY"],
                Aggregations = [new DataAggregation { Column = "AMOUNT", Function = "sum" }],
            },
        };

        AiOutputGrounding.ValidateCollectionChart(config, ["category", "amount"]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuildRepairSuffix_repeats_errors_and_allowlists()
    {
        var suffix = AiOutputGrounding.BuildRepairSuffix(
            ["SQL references unknown table 'customers'."],
            ["sales"],
            ["category", "amount"]);

        suffix.Should().Contain("customers");
        suffix.Should().Contain("sales");
        suffix.Should().Contain("category");
        suffix.Should().Contain("CORRECTION REQUIRED");
    }
}

public class AiSchemaContextTests
{
    [Fact]
    public void ToPromptJson_lists_allowlists_and_excludes_row_data()
    {
        var schema = new TableSchema
        {
            TableName = "sales",
            Columns =
            [
                new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            ],
        };

        var json = AiSchemaContext.ToPromptJson(schema);

        json.Should().Contain("allowedTables");
        json.Should().Contain("allowedColumns");
        json.Should().Contain("sales");
        json.Should().Contain("amount");
        json.Should().Contain("measure");
        json.Should().NotContain("rowData");
        json.Should().NotContain("sampleRows");
    }
}

public class AiChartPromptBuilderTests
{
    [Fact]
    public void System_prompt_includes_identifier_and_style_allowlists()
    {
        var schemaJson = AiSchemaContext.ToPromptJson(new TableSchema
        {
            TableName = "sales",
            Columns =
            [
                new ColumnSchema { ColumnName = "category", DataType = "text" },
                new ColumnSchema { ColumnName = "amount", DataType = "numeric" },
            ],
        });

        var prompt = AiChartPromptBuilder.BuildSystemPrompt(
            schemaJson,
            DbProvider.PostgreSql,
            prefabChartType: null,
            currentChartJson: null,
            allowedColors: ["var(--chart-1)", "#112233"]);

        prompt.Should().Contain("Allowed tables");
        prompt.Should().Contain("Allowed columns");
        prompt.Should().Contain("sales");
        prompt.Should().Contain("category");
        prompt.Should().Contain("Do NOT invent");
        prompt.Should().Contain("var(--chart-1)");
        prompt.Should().Contain("#112233");
        prompt.Should().Contain("never set params");
        prompt.Should().NotContain("\"params\": { \"paramKey\": value }");
    }

    [Fact]
    public void Collection_prompt_does_not_ask_the_model_for_params()
    {
        var prompt = AiChartPromptBuilder.BuildCollectionSystemPrompt(
            """{"table":"sales","allowedTables":["sales"],"allowedColumns":["category","amount"]}""",
            prefabChartType: "bar",
            allowedColors: ["var(--chart-1)"]);

        prompt.Should().Contain("Do NOT set customColors or params");
        prompt.Should().NotContain("styleConfig.params may only use");
        prompt.Should().Contain("var(--chart-1)");
    }
}

public class ChartStyleSanitizerHallucinationTests
{
    [Fact]
    public void Sanitize_drops_invented_variant_palette_colors_and_params()
    {
        using var doc = JsonDocument.Parse(
            """{"inventedParam":true,"showGrid":true,"cornerRadius":"huge"}""");

        var style = new ChartStyleConfig
        {
            Variant = "3d-exploded",
            Palette = "rainbow",
            Colors = ["red", "#zzzzzz", "var(--chart-1)"],
            Params = new Dictionary<string, JsonElement>
            {
                ["inventedParam"] = doc.RootElement.GetProperty("inventedParam").Clone(),
                ["showGrid"] = doc.RootElement.GetProperty("showGrid").Clone(),
                ["cornerRadius"] = doc.RootElement.GetProperty("cornerRadius").Clone(),
            },
        };

        var sanitized = ChartStyleSanitizer.Sanitize(style, "bar");

        sanitized.Should().NotBeNull();
        sanitized!.Variant.Should().BeNull();
        sanitized.Palette.Should().BeNull();
        sanitized.Colors.Should().BeEquivalentTo(["", "", "var(--chart-1)"]);
        sanitized.Params.Should().ContainKey("showGrid");
        sanitized.Params.Should().NotContainKey("inventedParam");
        sanitized.Params.Should().NotContainKey("cornerRadius");
    }

    [Fact]
    public void TakeAiControlledStyleFields_strips_params_and_clamps_colors()
    {
        var style = new ChartStyleConfig
        {
            Variant = "stacked",
            Colors = ["var(--chart-1)", "#ff00ff"],
            Params = new Dictionary<string, JsonElement>
            {
                ["showGrid"] = JsonDocument.Parse("true").RootElement.Clone(),
            },
        };

        var taken = ChartRefineMerger.TakeAiControlledStyleFields(
            style,
            "bar",
            ["var(--chart-1)", "var(--chart-2)"]);

        taken.Should().NotBeNull();
        taken!.Variant.Should().Be("stacked");
        taken.Params.Should().BeNull();
        taken.Colors.Should().BeEquivalentTo(["var(--chart-1)"]);
        taken.Palette.Should().BeNull();
    }
}
