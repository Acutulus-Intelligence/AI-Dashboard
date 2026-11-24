using Application.Services;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class ChartResultNormalizerTests
{
    private static AiChartConfig Config(
        string chartType = "bar",
        string xAxis = "category",
        params string[] yAxis) => new()
    {
        ChartType = chartType,
        XAxis = xAxis,
        YAxis = [.. yAxis],
        SqlQuery = "SELECT 1",
    };

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values)
    {
        var row = new Dictionary<string, object?>();
        foreach (var (key, value) in values)
            row[key] = value;
        return row;
    }

    [Fact]
    public void Axes_are_mapped_to_the_result_columns_case_insensitively()
    {
        var config = Config(xAxis: "Category", yAxis: ["Amount"]);
        var notes = new List<string>();
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows, notes).Should().BeTrue();

        config.XAxis.Should().Be("category");
        config.YAxis.Should().Equal("amount");
        notes.Should().Contain(n => n.Contains("Category"));
    }

    [Fact]
    public void Missing_y_axis_falls_back_to_a_numeric_result_column()
    {
        var config = Config(yAxis: ["not_returned"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.YAxis.Should().Equal("amount");
    }

    [Fact]
    public void Non_numeric_y_axes_are_dropped()
    {
        var config = Config(yAxis: ["label", "amount"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("label", "text"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.YAxis.Should().Equal("amount");
    }

    [Fact]
    public void A_result_without_numeric_columns_falls_back_to_a_table()
    {
        var config = Config(yAxis: ["label"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("label", "text")),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.ChartType.Should().Be("table");
    }

    [Fact]
    public void Unknown_x_axis_falls_back_to_the_first_categorical_column()
    {
        var config = Config(xAxis: "missing", yAxis: ["amount"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("id", 1), ("category", "A"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.XAxis.Should().Be("category");
    }

    [Fact]
    public void Empty_results_leave_the_config_unchanged()
    {
        var config = Config(xAxis: "category", yAxis: ["amount"]);

        ChartResultNormalizer.Normalize(config, []).Should().BeFalse();

        config.ChartType.Should().Be("bar");
        config.XAxis.Should().Be("category");
        config.YAxis.Should().Equal("amount");
    }

    [Fact]
    public void String_numbers_count_as_numeric()
    {
        var config = Config(yAxis: ["amount"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("amount", "12.5")),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.YAxis.Should().Equal("amount");
    }
}
