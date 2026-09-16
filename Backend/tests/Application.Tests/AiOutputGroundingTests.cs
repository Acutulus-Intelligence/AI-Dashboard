using Application.DTos.Request;
using Application.Services;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class AiOutputGroundingTests
{
    private static TableSchema SalesSchema() => new()
    {
        TableName = "sales",
        Columns =
        [
            new ColumnSchema { ColumnName = "id", DataType = "integer", IsNullable = false },
            new ColumnSchema { ColumnName = "category", DataType = "text", IsNullable = false },
            new ColumnSchema { ColumnName = "amount", DataType = "numeric", IsNullable = false },
            new ColumnSchema { ColumnName = "sold_at", DataType = "timestamp", IsNullable = false },
        ]
    };

    private static AiChartConfig Config(string sql, string x = "category", params string[] y) => new()
    {
        ChartType = "bar",
        Title = "Sales",
        XAxis = x,
        YAxis = [.. y],
        Aggregation = "sum",
        GroupBy = "category",
        SqlQuery = sql,
    };

    [Fact]
    public void GroundSqlChart_accepts_valid_select()
    {
        var config = Config(
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.SqlQuery.Should().Contain("FROM sales");
        result.Config.XAxis.Should().Be("category");
        result.Config.YAxis.Should().Equal("amount");
    }

    [Fact]
    public void GroundSqlChart_repairs_column_typo_in_sql_and_axes()
    {
        var config = Config(
            "SELECT categroy, SUM(amount) AS amount FROM sales GROUP BY categroy",
            x: "categroy",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.XAxis.Should().Be("category");
        result.Config.SqlQuery.Should().Contain("category");
        result.Config.SqlQuery.Should().NotContain("categroy");
        result.Notes.Should().Contain(n => n.Contains("categroy"));
    }

    [Fact]
    public void GroundSqlChart_rewrites_column_case_to_schema()
    {
        var config = Config(
            "SELECT Category, SUM(Amount) AS Amount FROM sales GROUP BY Category",
            x: "Category",
            y: "Amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.XAxis.Should().Be("category");
        result.Config.YAxis.Should().Equal("amount");
        result.Config.SqlQuery.Should().Contain("category");
        result.Config.SqlQuery.Should().Contain("amount");
    }

    [Fact]
    public void GroundSqlChart_repairs_compact_underscore_mismatch()
    {
        var config = Config(
            "SELECT soldat, SUM(amount) AS amount FROM sales GROUP BY soldat",
            x: "soldat",
            y: "amount");
        config.GroupBy = "soldat";

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.XAxis.Should().Be("sold_at");
        result.Config.SqlQuery.Should().Contain("sold_at");
    }

    [Fact]
    public void GroundSqlChart_rejects_invented_column()
    {
        var config = Config(
            "SELECT customer_name, SUM(amount) AS amount FROM sales GROUP BY customer_name",
            x: "customer_name",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("customer_name"));
    }

    [Fact]
    public void GroundSqlChart_rejects_invented_table()
    {
        var config = Config(
            "SELECT category, SUM(amount) AS amount FROM orders GROUP BY category",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("orders"));
    }

    [Fact]
    public void GroundSqlChart_repairs_table_typo()
    {
        var config = Config(
            "SELECT category, SUM(amount) AS amount FROM sale GROUP BY category",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.SqlQuery.Should().Contain("FROM sales");
    }

    [Fact]
    public void GroundSqlChart_allows_select_alias_on_axis()
    {
        var config = Config(
            "SELECT category, SUM(amount) AS total FROM sales GROUP BY category",
            y: "total");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.YAxis.Should().Equal("total");
    }

    [Fact]
    public void GroundSqlChart_does_not_treat_string_literals_as_identifiers()
    {
        var config = Config(
            "SELECT date_trunc('month', sold_at) AS month, SUM(amount) AS amount FROM sales GROUP BY 1",
            x: "month",
            y: "amount");
        config.GroupBy = null;

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue(because: string.Join("; ", result.Errors));
    }

    [Fact]
    public void GroundSqlChart_does_not_rewrite_literals_that_match_column_names()
    {
        var config = Config(
            "SELECT category, SUM(amount) AS amount FROM sales WHERE category = 'category' GROUP BY category",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config);

        result.IsGrounded.Should().BeTrue();
        result.Config.SqlQuery.Should().Contain("'category'");
    }

    [Fact]
    public void GroundSqlChart_falls_back_to_baseline_sql_on_refine()
    {
        var baseline = new ChartBaseline(
            "Sales",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category");

        var config = Config(
            "SELECT customer_name, SUM(revenue) AS revenue FROM customers GROUP BY customer_name",
            x: "category",
            y: "amount");

        var result = AiOutputGrounding.GroundSqlChart(SalesSchema(), config, baseline);

        result.IsGrounded.Should().BeTrue();
        result.Config.SqlQuery.Should().Contain("FROM sales");
        result.Notes.Should().Contain(n => n.Contains("kept baseline SQL"));
    }

    [Fact]
    public void GroundCollectionChart_repairs_case_and_rejects_unknown_filter()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            Title = "Sales",
            XAxis = "Category",
            YAxis = ["Amount"],
            Aggregation = "sum",
            GroupBy = "Category",
            DataModel = new DataQueryModel
            {
                Filters = [new DataFilter { Column = "categroy", Operator = "eq", Value = "A" }],
                GroupBy = ["Category"],
                Aggregations = [new DataAggregation { Column = "Amount", Function = "sum" }],
                OrderBy = [new DataOrderBy { Column = "Amount", Direction = "desc" }],
            }
        };

        var result = AiOutputGrounding.GroundCollectionChart(["category", "amount"], config);

        result.Config.XAxis.Should().Be("category");
        result.Config.DataModel!.GroupBy.Should().Equal("category");
        result.Config.DataModel.Aggregations[0].Column.Should().Be("amount");
        result.Config.DataModel.Filters[0].Column.Should().Be("category");
        result.IsGrounded.Should().BeTrue();
    }

    [Fact]
    public void GroundCollectionChart_rejects_invented_column()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            Title = "Sales",
            XAxis = "customer_name",
            YAxis = ["amount"],
            DataModel = new DataQueryModel
            {
                GroupBy = ["customer_name"],
                Aggregations = [new DataAggregation { Column = "amount", Function = "sum" }],
            }
        };

        var result = AiOutputGrounding.GroundCollectionChart(["category", "amount"], config);

        result.IsGrounded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("customer_name"));
    }

    [Fact]
    public void GroundCollectionChart_rejects_unknown_operator()
    {
        var config = new AiChartConfig
        {
            ChartType = "bar",
            XAxis = "category",
            YAxis = ["amount"],
            DataModel = new DataQueryModel
            {
                Filters = [new DataFilter { Column = "category", Operator = "regex", Value = "A" }],
                GroupBy = ["category"],
                Aggregations = [new DataAggregation { Column = "amount", Function = "sum" }],
            }
        };

        var result = AiOutputGrounding.GroundCollectionChart(["category", "amount"], config);

        result.IsGrounded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("regex"));
    }

    [Fact]
    public void AiSchemaPayload_includes_allowlists()
    {
        var json = AiSchemaPayload.Serialize(SalesSchema());

        json.Should().Contain("allowedTableName");
        json.Should().Contain("allowedColumnNames");
        json.Should().Contain("sales");
        json.Should().Contain("sold_at");
        json.Should().Contain("Never invent");
    }

    [Fact]
    public void BuildRepairPrompt_lists_errors_and_previous_json()
    {
        var grounding = new AiGroundingResult
        {
            Config = Config("SELECT nope FROM sales", y: "amount"),
            Errors = ["SQL references unknown identifier 'nope'. Allowed columns: amount, category, id, sold_at."]
        };

        var prompt = AiOutputGrounding.BuildRepairPrompt(grounding, """{"chartType":"bar"}""");

        prompt.Should().Contain("not grounded");
        prompt.Should().Contain("nope");
        prompt.Should().Contain("Previous JSON");
        prompt.Should().Contain("chartType");
    }

    [Theory]
    [InlineData("column \"customer_name\" does not exist", true)]
    [InlineData("Unknown column 'foo' in 'field list'", true)]
    [InlineData("Query exceeded the maximum row limit of 5000.", false)]
    public void IsLikelySchemaError_detects_missing_identifiers(string message, bool expected)
    {
        AiOutputGrounding.IsLikelySchemaError(message).Should().Be(expected);
    }
}
