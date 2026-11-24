using System.Text.Json;
using Application.Services;
using Domain.Charts;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class ChartLintTests
{
    private static AiChartConfig Config(string chartType, params string[] yAxis) => new()
    {
        ChartType = chartType,
        XAxis = "category",
        YAxis = [.. yAxis],
        Aggregation = "sum",
        SqlQuery = "SELECT category, amount FROM sales",
    };

    private static List<Dictionary<string, object?>> Rows(
        int count,
        string x = "category",
        double offset = 0,
        params string[] measures)
    {
        var series = measures.Length == 0 ? new[] { "amount" } : measures;
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var row = new Dictionary<string, object?> { [x] = $"c{i}" };
                for (var m = 0; m < series.Length; m++)
                    row[series[m]] = offset + i + 1 + (m * 10);
                return row;
            })
            .ToList();
    }

    private static bool? BoolParam(AiChartConfig config, string key) =>
        config.StyleConfig?.Params is { } p && p.TryGetValue(key, out var value)
            ? value.GetBoolean()
            : null;

    [Fact]
    public void Multi_series_turns_on_the_legend_when_unset()
    {
        var config = Config("bar", "amount", "quantity");

        ChartLint.Apply(config, Rows(3, measures: ["amount", "quantity"]), allowTypeChange: true);

        BoolParam(config, "showLegend").Should().BeTrue();
    }

    [Fact]
    public void Few_rows_turn_on_value_labels()
    {
        var config = Config("bar", "amount");

        ChartLint.Apply(config, Rows(5), allowTypeChange: true);

        BoolParam(config, "showLabels").Should().BeTrue();
    }

    [Fact]
    public void An_explicit_false_legend_from_the_ai_is_respected()
    {
        var config = Config("bar", "amount", "quantity");
        config.StyleConfig = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["showLegend"] = JsonSerializer.SerializeToElement(false),
            },
        };

        ChartLint.Apply(config, Rows(3, measures: ["amount", "quantity"]), allowTypeChange: true);

        BoolParam(config, "showLegend").Should().BeFalse();
    }

    [Fact]
    public void Pie_with_more_than_eight_slices_keeps_the_top_eight_and_groups_the_rest()
    {
        var config = Config("pie", "amount");
        var notes = new List<string>();

        ChartLint.Apply(config, Rows(12), allowTypeChange: true, notes);

        config.ChartType.Should().Be("pie");
        config.StyleConfig!.Params!["topN"].GetInt32().Should().Be(8);
        notes.Should().Contain(n => n.Contains("Other"));
    }

    [Fact]
    public void Pie_respects_an_explicit_top_n()
    {
        var config = Config("pie", "amount");
        config.StyleConfig = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["topN"] = JsonSerializer.SerializeToElement(5),
            },
        };

        ChartLint.Apply(config, Rows(12), allowTypeChange: true);

        config.ChartType.Should().Be("pie");
        config.StyleConfig.Params!["topN"].GetInt32().Should().Be(5);
    }

    [Fact]
    public void Pie_with_more_than_eight_slices_is_kept_when_the_user_chose_it()
    {
        var config = Config("pie", "amount");

        ChartLint.Apply(config, Rows(12), allowTypeChange: false);

        config.ChartType.Should().Be("pie");
    }

    [Fact]
    public void Line_with_fewer_than_three_points_becomes_a_bar_chart()
    {
        var config = Config("line", "amount");

        ChartLint.Apply(config, Rows(2), allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Combo_with_a_single_measure_becomes_a_bar_chart()
    {
        var config = Config("combo", "amount");

        ChartLint.Apply(config, Rows(5), allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Scatter_with_fewer_than_three_rows_becomes_a_table()
    {
        var config = Config("scatter", "amount");

        ChartLint.Apply(config, Rows(2), allowTypeChange: true);

        config.ChartType.Should().Be("table");
    }

    [Fact]
    public void No_numeric_values_falls_back_to_table_even_for_an_explicit_type()
    {
        var config = Config("pie", "label");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["label"] = "text" },
            new() { ["category"] = "b", ["label"] = "more text" },
        };

        ChartLint.Apply(config, rows, allowTypeChange: false);

        config.ChartType.Should().Be("table");
    }

    [Fact]
    public void Long_category_labels_switch_bars_to_horizontal()
    {
        var config = Config("bar", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "An extremely long category name", ["amount"] = 10 },
            new() { ["category"] = "Another extremely long name", ["amount"] = 20 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.StyleConfig!.Variant.Should().Be("horizontal");
    }

    [Fact]
    public void Table_does_not_receive_chart_only_params()
    {
        var config = Config("table", "amount", "quantity");

        ChartLint.Apply(config, Rows(3, measures: ["amount", "quantity"]), allowTypeChange: true);

        config.StyleConfig?.Params.Should().BeNull();
    }

    [Fact]
    public void Heatmap_with_two_dimensions_is_kept()
    {
        var config = new AiChartConfig
        {
            ChartType = "heatmap",
            XAxis = "region",
            GroupBy = "month",
            YAxis = ["amount"],
            SqlQuery = "SELECT 1",
        };
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["region"] = "North", ["month"] = "Jan", ["amount"] = 10 },
            new() { ["region"] = "South", ["month"] = "Jan", ["amount"] = 20 },
            new() { ["region"] = "North", ["month"] = "Feb", ["amount"] = 30 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("heatmap");
    }

    [Fact]
    public void Heatmap_without_a_second_dimension_becomes_a_bar_chart()
    {
        var config = new AiChartConfig
        {
            ChartType = "heatmap",
            XAxis = "category",
            YAxis = ["amount"],
            SqlQuery = "SELECT 1",
        };

        ChartLint.Apply(config, Rows(5), allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Map_with_a_numeric_axis_and_no_coordinates_becomes_a_bar_chart()
    {
        var config = Config("map", "amount");
        config.XAxis = "amount";
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["amount"] = 10 },
            new() { ["amount"] = 20 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Map_with_place_names_is_kept()
    {
        var config = Config("map", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "Sweden", ["amount"] = 10 },
            new() { ["category"] = "Norway", ["amount"] = 20 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("map");
    }

    [Fact]
    public void Map_with_coordinates_is_kept()
    {
        var config = Config("map", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["latitude"] = 59.3, ["longitude"] = 18.1, ["amount"] = 10 },
            new() { ["latitude"] = 60.4, ["longitude"] = 5.3, ["amount"] = 20 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("map");
    }

    [Fact]
    public void Pivot_with_two_dimensions_is_kept()
    {
        var config = Config("pivot", "amount");
        config.GroupBy = "month";
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "North", ["month"] = "Jan", ["amount"] = 10 },
            new() { ["category"] = "South", ["month"] = "Feb", ["amount"] = 20 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("pivot");
    }

    [Fact]
    public void Pivot_without_a_second_dimension_becomes_a_bar_chart()
    {
        var config = Config("pivot", "amount");

        ChartLint.Apply(config, Rows(5), allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Funnel_with_positive_stages_is_kept()
    {
        var config = Config("funnel", "amount");

        ChartLint.Apply(config, Rows(4), allowTypeChange: true);

        config.ChartType.Should().Be("funnel");
    }

    [Fact]
    public void Funnel_with_negative_values_becomes_a_bar_chart()
    {
        var config = Config("funnel", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["amount"] = 10 },
            new() { ["category"] = "b", ["amount"] = -5 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Waterfall_with_signed_changes_is_kept()
    {
        var config = Config("waterfall", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["amount"] = 10 },
            new() { ["category"] = "b", ["amount"] = -5 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("waterfall");
    }

    [Fact]
    public void Waterfall_with_only_zero_changes_becomes_a_table()
    {
        var config = Config("waterfall", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["amount"] = 0 },
            new() { ["category"] = "b", ["amount"] = 0 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("table");
    }

    [Fact]
    public void Treemap_with_negative_values_becomes_a_bar_chart()
    {
        var config = Config("treemap", "amount");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["amount"] = 10 },
            new() { ["category"] = "b", ["amount"] = -5 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Treemap_with_a_single_category_becomes_a_bar_chart()
    {
        var config = Config("treemap", "amount");

        ChartLint.Apply(config, Rows(1), allowTypeChange: true);

        config.ChartType.Should().Be("bar");
    }

    [Fact]
    public void Count_like_measures_get_whole_numbers()
    {
        var config = Config("bar", "order_count");

        ChartLint.Apply(config, Rows(4, measures: ["order_count"]), allowTypeChange: true);

        config.StyleConfig!.Decimals.Should().Be(0);
    }

    [Fact]
    public void Percent_like_measures_get_a_suffix_when_values_are_on_a_percent_scale()
    {
        var config = Config("bar", "conversion_percent");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["conversion_percent"] = 42.5 },
            new() { ["category"] = "b", ["conversion_percent"] = 55.2 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.StyleConfig!.ValueSuffix.Should().Be("%");
        config.StyleConfig.Decimals.Should().Be(1);
    }

    [Fact]
    public void Percent_like_measures_do_not_get_a_suffix_for_fraction_values()
    {
        var config = Config("bar", "conversion_percent");
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["category"] = "a", ["conversion_percent"] = 0.42 },
            new() { ["category"] = "b", ["conversion_percent"] = 0.55 },
        };

        ChartLint.Apply(config, rows, allowTypeChange: true);

        config.StyleConfig?.ValueSuffix.Should().BeNull();
    }

    [Fact]
    public void Explicit_formatting_is_not_overridden_by_smart_defaults()
    {
        var config = Config("bar", "order_count");
        config.StyleConfig = new ChartStyleConfig { Decimals = 2, ValueSuffix = " st" };

        ChartLint.Apply(config, Rows(4, measures: ["order_count"]), allowTypeChange: true);

        config.StyleConfig.Decimals.Should().Be(2);
        config.StyleConfig.ValueSuffix.Should().Be(" st");
    }
}
