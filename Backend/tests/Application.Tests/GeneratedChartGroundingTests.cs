using Application.Interfaces;
using Application.Services;
using Domain.Charts;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class GeneratedChartGroundingTests
{
    private static readonly TableSchema SalesSchema = new()
    {
        TableName = "sales",
        Columns =
        [
            new ColumnSchema { ColumnName = "category", DataType = "text", IsNullable = false },
            new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
        ],
    };

    private static readonly string[] Allowlist = ["var(--chart-1)", "var(--chart-2)"];

    [Fact]
    public void ValidateSqlChart_accepts_schema_grounded_select()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "category",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
        };

        GeneratedChartGrounding.ValidateSqlChart(config, SalesSchema, new SelectOnlyValidator())
            .Should().BeNull();
    }

    [Fact]
    public void ValidateSqlChart_rejects_invented_column()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "customer_name",
            YAxis = ["revenue"],
            SqlQuery = "SELECT customer_name, SUM(revenue) AS revenue FROM sales GROUP BY customer_name",
        };

        var error = GeneratedChartGrounding.ValidateSqlChart(config, SalesSchema, new SelectOnlyValidator());

        error.Should().NotBeNull();
        error.Should().Contain("customer_name");
    }

    [Fact]
    public void ValidateSqlChart_rejects_invented_axis_even_when_sql_is_grounded()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "category",
            YAxis = ["revenue"],
            SqlQuery = "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
        };

        var error = GeneratedChartGrounding.ValidateSqlChart(config, SalesSchema, new SelectOnlyValidator());

        error.Should().NotBeNull();
        error.Should().Contain("yAxis");
        error.Should().Contain("revenue");
    }

    [Fact]
    public void ValidateSqlChart_allows_select_alias_as_yAxis()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "category",
            YAxis = ["total"],
            SqlQuery = "SELECT category, SUM(amount) AS total FROM sales GROUP BY category",
        };

        GeneratedChartGrounding.ValidateSqlChart(config, SalesSchema, new SelectOnlyValidator())
            .Should().BeNull();
    }

    [Fact]
    public void ValidateSqlChart_rejects_unknown_chart_type()
    {
        var config = new AiChartConfig
        {
            ChartType = "heatmap",
            XAxis = "category",
            YAxis = ["amount"],
            SqlQuery = "SELECT category, amount FROM sales",
        };

        var error = GeneratedChartGrounding.ValidateSqlChart(config, SalesSchema, new SelectOnlyValidator());

        error.Should().Contain("heatmap");
    }

    [Fact]
    public void ValidateStyle_flags_invented_hex_and_palette()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            StyleConfig = new ChartStyleConfig
            {
                Palette = "rainbow",
                Colors = ["#ff00aa"],
                Variant = "spiral",
            },
        };

        var error = GeneratedChartGrounding.ValidateStyle(config, Allowlist);

        error.Should().NotBeNull();
        error.Should().Match(e =>
            e.Contains("rainbow") || e.Contains("#ff00aa") || e.Contains("spiral"));
    }

    [Fact]
    public void ValidateStyle_accepts_allowlisted_color()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            StyleConfig = new ChartStyleConfig
            {
                Colors = ["var(--chart-1)"],
                Variant = "stacked",
            },
        };

        GeneratedChartGrounding.ValidateStyle(config, Allowlist).Should().BeNull();
    }

    private sealed class SelectOnlyValidator : ISqlValidator
    {
        public bool IsSelectOnly(string sql, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                errorMessage = "empty";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
