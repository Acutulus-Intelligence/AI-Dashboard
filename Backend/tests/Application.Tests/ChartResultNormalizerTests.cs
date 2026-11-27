using System.Text.Json;
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

    /// <summary>
    /// Mirrors a chart result that has round-tripped through the JSON cache:
    /// every cell value is deserialized as a JsonElement.
    /// </summary>
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

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
    public void Group_by_is_mapped_to_the_result_column_or_cleared()
    {
        var config = Config(xAxis: "category", yAxis: ["amount"]);
        config.GroupBy = "Region";
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("region", "North"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.GroupBy.Should().Be("region");

        config.GroupBy = "missing";
        ChartResultNormalizer.Normalize(config, rows);
        config.GroupBy.Should().BeNull();
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

    [Fact]
    public void Numeric_values_need_a_strict_majority_to_count_as_a_measure()
    {
        var mixed = new List<Dictionary<string, object?>>
        {
            Row(("amount", "12.5")),
            Row(("amount", "not a number")),
            Row(("amount", null)),
        };

        ChartResultNormalizer.IsNumericColumn(mixed, "amount").Should().BeFalse();

        var mostlyNumeric = new List<Dictionary<string, object?>>
        {
            Row(("amount", "12.5")),
            Row(("amount", 4)),
            Row(("amount", "N/A")),
        };

        ChartResultNormalizer.IsNumericColumn(mostlyNumeric, "amount").Should().BeTrue();
    }

    [Fact]
    public void Explicit_identifier_columns_are_kept_when_the_user_chose_them()
    {
        var config = Config(yAxis: ["customer_id"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("customer_id", 1001)),
            Row(("category", "B"), ("customer_id", 1002)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.ChartType.Should().Be("bar");
        config.YAxis.Should().Equal("customer_id");
    }

    [Fact]
    public void Identifier_columns_are_not_chosen_as_an_automatic_measure()
    {
        var config = Config(yAxis: ["not_returned"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("category", "A"), ("customer_id", 1001)),
            Row(("category", "B"), ("customer_id", 1002)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.ChartType.Should().Be("table");
    }

    [Fact]
    public void Identifier_columns_are_not_chosen_as_the_x_axis()
    {
        var config = Config(xAxis: "missing", yAxis: ["amount"]);
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("id", 1), ("label", "A"), ("amount", 10)),
        };

        ChartResultNormalizer.Normalize(config, rows);

        config.XAxis.Should().Be("label");
    }

    [Fact]
    public void Json_element_numbers_from_the_cache_count_as_numeric()
    {
        // Redis-cached chart rows deserialize every value as JsonElement.
        var config = Config(xAxis: "stage", yAxis: ["users"]);
        var notes = new List<string>();
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("stage", Json("\"Visitors\"")), ("users", Json("10000"))),
            Row(("stage", Json("\"Paid\"")), ("users", Json("620"))),
        };

        ChartResultNormalizer.Normalize(config, rows, notes).Should().BeTrue();

        config.ChartType.Should().Be("bar");
        config.YAxis.Should().Equal("users");
        notes.Should().BeEmpty();
    }

    [Fact]
    public void Json_element_string_numbers_count_as_numeric()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("amount", Json("\"12.5\""))),
        };

        ChartResultNormalizer.IsNumericColumn(rows, "amount").Should().BeTrue();
    }

    [Fact]
    public void Json_element_null_values_are_ignored_when_sampling()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            Row(("amount", Json("10"))),
            Row(("amount", Json("null"))),
            Row(("amount", Json("null"))),
        };

        ChartResultNormalizer.IsNumericColumn(rows, "amount").Should().BeTrue();
    }
}
