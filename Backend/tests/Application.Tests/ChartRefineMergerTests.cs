using Application.DTos.Request;
using Application.Services;
using Domain.Charts;
using Domain.Models;
using FluentAssertions;

namespace Application.Tests;

public class ChartRefineMergerTests
{
    private static readonly string[] Allowlist =
    [
        "var(--chart-1)",
        "var(--chart-2)",
        "var(--chart-3)",
    ];

    [Theory]
    [InlineData("make the colours nicer")]
    [InlineData("use colors for each series")]
    [InlineData("I want coloured bars")]
    [InlineData("styled differently")]
    [InlineData("show values in dollars")]
    [InlineData("as a percentage")]
    [InlineData("make it monochrome")]
    [InlineData("visa i kronor")]
    [InlineData("make amount blue")]
    [InlineData("switch to warm palette")]
    [InlineData("round to 2 decimals")]
    public void RequestsStyleChange_detects_style_phrasing(string prompt)
    {
        ChartRefineMerger.RequestsStyleChange(prompt).Should().BeTrue();
    }

    [Theory]
    [InlineData("show last 30 days")]
    [InlineData("filter by region")]
    [InlineData("ordered by date")]
    [InlineData("group by month")]
    public void RequestsStyleChange_ignores_data_only_prompts(string prompt)
    {
        ChartRefineMerger.RequestsStyleChange(prompt).Should().BeFalse();
    }

    [Fact]
    public void ClampColorsToAllowlist_preserves_empty_slots()
    {
        var input = new List<string> { "var(--chart-1)", "", "var(--chart-3)" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().BeEquivalentTo(["var(--chart-1)", "", "var(--chart-3)"]);
    }

    [Fact]
    public void ClampColorsToAllowlist_allows_duplicate_colors_for_multiple_series()
    {
        var input = new List<string> { "var(--chart-1)", "var(--chart-1)" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().BeEquivalentTo(["var(--chart-1)", "var(--chart-1)"]);
    }

    [Fact]
    public void ClampColorsToAllowlist_replaces_invalid_with_empty_slot()
    {
        var input = new List<string> { "var(--chart-1)", "#bad", "var(--chart-2)" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().BeEquivalentTo(["var(--chart-1)", "", "var(--chart-2)"]);
    }

    [Fact]
    public void ClampColorsToAllowlist_uses_canonical_allowlist_casing()
    {
        var input = new List<string> { "VAR(--chart-1)" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().BeEquivalentTo(["var(--chart-1)"]);
    }

    [Theory]
    [InlineData("filter the red category")]
    [InlineData("round up sales by region")]
    public void RequestsStyleChange_treats_color_words_in_data_prompts_as_style(string prompt)
    {
        // Known false-positive tradeoff: colour/format words open style merge even in data phrasing.
        // AI output is still clamped/sanitized; baseline style is only replaced when the model returns changes.
        ChartRefineMerger.RequestsStyleChange(prompt).Should().BeTrue();
    }

    [Fact]
    public void HasNonEmptyColorSlot_false_when_all_empty()
    {
        ChartRefineMerger.HasNonEmptyColorSlot(["", ""]).Should().BeFalse();
    }

    [Fact]
    public void HasNonEmptyColorSlot_true_when_any_slot_set()
    {
        ChartRefineMerger.HasNonEmptyColorSlot(["", "var(--chart-1)"]).Should().BeTrue();
    }

    [Theory]
    [InlineData("Colour 2", "var(--chart-2)")]
    [InlineData("color_3", "var(--chart-3)")]
    [InlineData("blue", "var(--chart-1)")]
    [InlineData("orange", "var(--chart-2)")]
    public void SnapColorToAllowlist_maps_labels_and_hue_words(string raw, string expected)
    {
        ChartRefineMerger.SnapColorToAllowlist(raw, Allowlist).Should().Be(expected);
    }

    [Fact]
    public void SnapColorToAllowlist_maps_near_hex_onto_theme_token()
    {
        ChartRefineMerger.SnapColorToAllowlist("#3b82f8", Allowlist)
            .Should().Be("var(--chart-1)");
    }

    [Fact]
    public void SnapColorToAllowlist_rejects_far_hex()
    {
        ChartRefineMerger.SnapColorToAllowlist("#ff00aa", Allowlist).Should().BeNull();
    }

    [Fact]
    public void Apply_keeps_baseline_style_for_a_data_only_prompt()
    {
        var baseline = Baseline(Style(
            variant: "stacked",
            palette: "warm",
            valuePrefix: "€",
            decimals: 1));
        var ai = AiResult(new ChartStyleConfig
        {
            Palette = "cool",
            Colors = ["var(--chart-1)"],
            ValuePrefix = "$",
            Decimals = 0,
            DecimalMode = "truncate",
        });

        var result = ChartRefineMerger.Apply(baseline, ai, "show last 30 days", Allowlist);

        result.SqlQuery.Should().Contain("30 days");
        result.StyleConfig!.Variant.Should().Be("stacked");
        result.StyleConfig.Palette.Should().Be("warm");
        result.StyleConfig.Colors.Should().BeNull();
        result.StyleConfig.ValuePrefix.Should().Be("€");
        result.StyleConfig.Decimals.Should().Be(1);
        result.StyleConfig.DecimalMode.Should().Be("round");
    }

    [Fact]
    public void Apply_updates_style_when_the_prompt_asks_for_colour_and_currency_labels()
    {
        var baseline = Baseline(Style(
            variant: "stacked",
            palette: "warm",
            valuePrefix: "€",
            decimals: 1));
        var ai = AiResult(new ChartStyleConfig
        {
            Colors = ["blue"],
            ValuePrefix = "$",
        });

        var result = ChartRefineMerger.Apply(baseline, ai, "make it blue with $ labels", Allowlist);

        result.StyleConfig!.Variant.Should().Be("stacked");
        result.StyleConfig.Palette.Should().BeNull();
        result.StyleConfig.Colors.Should().Equal("var(--chart-1)");
        result.StyleConfig.ValuePrefix.Should().Be("$");
        result.StyleConfig.Decimals.Should().Be(1);
        result.StyleConfig.DecimalMode.Should().Be("round");
    }

    [Fact]
    public void Apply_strips_colours_and_value_format_when_the_chart_is_a_table()
    {
        var baseline = Baseline(Style(variant: "raw", palette: "warm", valuePrefix: "€", decimals: 2), "table");
        var ai = AiResult(new ChartStyleConfig
        {
            Variant = "summary",
            Palette = "cool",
            Colors = ["blue"],
            ValuePrefix = "$",
            ValueSuffix = "%",
            Decimals = 0,
        }, "table");

        var result = ChartRefineMerger.Apply(baseline, ai, "make it blue with $ labels", Allowlist);

        result.ChartType.Should().Be("table");
        result.StyleConfig!.Variant.Should().Be("summary");
        result.StyleConfig.Palette.Should().BeNull();
        result.StyleConfig.Colors.Should().BeNull();
        result.StyleConfig.ValuePrefix.Should().BeNull();
        result.StyleConfig.ValueSuffix.Should().BeNull();
        result.StyleConfig.Decimals.Should().BeNull();
    }

    private static ChartStyleConfig Style(string variant, string palette, string valuePrefix, int decimals) =>
        new()
        {
            Variant = variant,
            Palette = palette,
            ValuePrefix = valuePrefix,
            Decimals = decimals,
            DecimalMode = "round",
        };

    private static ChartBaseline Baseline(ChartStyleConfig style, string chartType = "bar") =>
        new(
            "Sales",
            chartType,
            "month",
            ["amount"],
            "sum",
            "month",
            "SELECT month, SUM(amount) AS amount FROM sales GROUP BY month",
            style);

    private static AiChartConfig AiResult(ChartStyleConfig style, string chartType = "bar") =>
        new()
        {
            ChartType = chartType,
            Title = "Sales",
            XAxis = "month",
            YAxis = ["amount"],
            Aggregation = "sum",
            GroupBy = "month",
            SqlQuery = "SELECT month, SUM(amount) AS amount FROM sales WHERE sold_at >= CURRENT_DATE - INTERVAL '30 days' GROUP BY month",
            StyleConfig = style,
        };
}
