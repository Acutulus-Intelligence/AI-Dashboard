using Application.Services;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class DataQueryModelGroundingTests
{
    private static readonly string[] Columns = ["category", "amount"];

    [Fact]
    public void Accepts_grouped_sum_on_schema_columns()
    {
        var model = new DataQueryModel
        {
            GroupBy = ["category"],
            Aggregations = [new DataAggregation { Column = "amount", Function = "sum" }],
            OrderBy = [new DataOrderBy { Column = "amount", Direction = "desc" }],
            Limit = 10,
        };

        DataQueryModelGrounding.Validate(model, Columns).Should().BeNull();
    }

    [Fact]
    public void Rejects_invented_filter_column()
    {
        var model = new DataQueryModel
        {
            Filters = [new DataFilter { Column = "customer_email", Operator = "eq", Value = "a@b.c" }],
        };

        var error = DataQueryModelGrounding.Validate(model, Columns);

        error.Should().Contain("customer_email");
        error.Should().Contain("Allowed columns");
    }

    [Fact]
    public void Rejects_invented_yAxis()
    {
        var model = new DataQueryModel
        {
            GroupBy = ["category"],
            Aggregations = [new DataAggregation { Column = "amount", Function = "sum" }],
        };
        var config = new AiChartConfig
        {
            XAxis = "category",
            YAxis = ["revenue"],
            GroupBy = "category",
        };

        var error = DataQueryModelGrounding.Validate(model, Columns, config);

        error.Should().Contain("yAxis");
        error.Should().Contain("revenue");
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var model = new DataQueryModel
        {
            GroupBy = ["Category"],
            Aggregations = [new DataAggregation { Column = "AMOUNT", Function = "SUM" }],
        };

        DataQueryModelGrounding.Validate(model, Columns).Should().BeNull();
    }

    [Fact]
    public void Rejects_unknown_operator()
    {
        var model = new DataQueryModel
        {
            Filters = [new DataFilter { Column = "category", Operator = "regex", Value = "A.*" }],
        };

        DataQueryModelGrounding.Validate(model, Columns).Should().Contain("operator");
    }
}
