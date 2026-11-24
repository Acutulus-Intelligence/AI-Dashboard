using System.Text.Json;
using Application.Services;
using Domain.Charts;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class ChartResultShaperTests
{
    private static AiChartConfig Config(string chartType, int topN, params string[] yAxis)
    {
        var config = new AiChartConfig
        {
            ChartType = chartType,
            XAxis = "category",
            YAxis = yAxis.Length == 0 ? ["amount"] : [.. yAxis],
            Aggregation = "sum",
            SqlQuery = "SELECT 1",
        };

        if (topN > 0)
        {
            config.StyleConfig = new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["topN"] = JsonSerializer.SerializeToElement(topN),
                },
            };
        }

        return config;
    }

    private static List<Dictionary<string, object?>> Rows(params (string Category, object? Amount)[] values) =>
        values
            .Select(value => new Dictionary<string, object?>
            {
                ["category"] = value.Category,
                ["amount"] = value.Amount,
            })
            .ToList();

    [Fact]
    public void Keeps_the_largest_categories_and_groups_the_rest_into_other()
    {
        var rows = Rows(("a", 5), ("b", 30), ("c", 10), ("d", 20), ("e", 1));

        var shaped = ChartResultShaper.Shape(Config("bar", 3), rows);

        shaped.Should().HaveCount(4);
        shaped.Take(3).Select(r => r["category"]?.ToString()).Should().Equal("b", "d", "c");
        shaped[3]["category"].Should().Be(ChartResultShaper.OtherLabel);
        shaped[3]["amount"].Should().Be(6d); // 5 + 1
    }

    [Fact]
    public void Other_preserves_the_total()
    {
        var rows = Rows(("a", 5), ("b", 30), ("c", 10), ("d", 20), ("e", 1));

        var shaped = ChartResultShaper.Shape(Config("pie", 2), rows);

        var total = shaped.Sum(row => Convert.ToDouble(row["amount"]));
        total.Should().Be(66d);
    }

    [Fact]
    public void Top_n_of_zero_or_more_than_the_row_count_leaves_rows_untouched()
    {
        var rows = Rows(("a", 5), ("b", 30));

        ChartResultShaper.Shape(Config("bar", 0), rows).Should().HaveCount(2);
        ChartResultShaper.Shape(Config("bar", 10), rows).Should().HaveCount(2);
    }

    [Fact]
    public void Unsupported_chart_types_are_not_shaped()
    {
        var rows = Rows(("a", 5), ("b", 30), ("c", 10), ("d", 20));

        ChartResultShaper.Shape(Config("line", 2), rows).Should().HaveCount(4);
    }

    [Fact]
    public void Null_values_sort_last()
    {
        var rows = Rows(("a", null), ("b", 30), ("c", 10), ("d", null));

        var shaped = ChartResultShaper.Shape(Config("bar", 2), rows);

        shaped.Take(2).Select(r => r["category"]?.ToString()).Should().Equal("b", "c");
        shaped[2]["category"].Should().Be(ChartResultShaper.OtherLabel);
    }

    [Fact]
    public void The_input_list_is_never_mutated()
    {
        var rows = Rows(("a", 5), ("b", 30), ("c", 10), ("d", 20));

        ChartResultShaper.Shape(Config("bar", 2), rows);

        rows.Should().HaveCount(4);
    }

    [Fact]
    public void A_missing_x_axis_or_measure_skips_shaping()
    {
        var rows = Rows(("a", 5), ("b", 30), ("c", 10));
        var missingX = Config("bar", 2);
        missingX.XAxis = "missing";

        ChartResultShaper.Shape(missingX, rows).Should().HaveCount(3);

        var missingY = Config("bar", 2);
        missingY.YAxis = ["missing"];

        ChartResultShaper.Shape(missingY, rows).Should().HaveCount(3);
    }
}
