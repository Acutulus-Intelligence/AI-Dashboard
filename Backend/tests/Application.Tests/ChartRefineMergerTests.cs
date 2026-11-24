using Application.DTos.Request;
using Application.Services;
using Domain.Charts;
using Domain.Models;
using FluentAssertions;
using System.Text.Json;

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

    [Theory]
    [InlineData("show the legend")]
    [InlineData("visa teckenförklaring")]
    [InlineData("add value labels")]
    [InlineData("visa etiketter")]
    public void RequestsStyleChange_detects_legend_and_label_phrasing(string prompt)
    {
        ChartRefineMerger.RequestsStyleChange(prompt).Should().BeTrue();
    }

    [Fact]
    public void TakeAiControlledStyleFields_keeps_only_legend_and_label_params()
    {
        var style = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["showLegend"] = JsonSerializer.SerializeToElement(true),
                ["showLabels"] = JsonSerializer.SerializeToElement(false),
                ["showGrid"] = JsonSerializer.SerializeToElement(true),
                ["sparklineHeight"] = JsonSerializer.SerializeToElement(50),
            },
        };

        var result = ChartRefineMerger.TakeAiControlledStyleFields(style, "bar");

        result.Should().NotBeNull();
        result!.Params.Should().NotBeNull();
        result.Params!.Keys.Should().BeEquivalentTo(["showLegend", "showLabels"]);
    }

    [Fact]
    public void Table_drops_ai_params()
    {
        var style = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["showLegend"] = JsonSerializer.SerializeToElement(true),
            },
        };

        ChartRefineMerger.TakeAiControlledStyleFields(style, "table").Should().BeNull();
    }

    [Fact]
    public void Refine_without_a_label_request_preserves_baseline_params()
    {
        var baseline = new ChartBaseline(
            "Sales", "bar", "category", ["amount", "quantity"], "sum", "category",
            "SELECT category, amount, quantity FROM sales",
            new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["showLegend"] = JsonSerializer.SerializeToElement(false),
                },
            });
        var ai = new AiChartConfig
        {
            ChartType = "bar",
            Title = "Sales",
            XAxis = "category",
            YAxis = ["amount", "quantity"],
            StyleConfig = new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["showLabels"] = JsonSerializer.SerializeToElement(true),
                },
            },
        };

        var result = ChartRefineMerger.Apply(baseline, ai, "order by amount", Allowlist);

        result.StyleConfig!.Params.Should().ContainKey("showLegend");
        result.StyleConfig.Params.Should().NotContainKey("showLabels");
    }

    [Fact]
    public void Refine_with_a_label_request_merges_ai_params()
    {
        var baseline = new ChartBaseline(
            "Sales", "bar", "category", ["amount", "quantity"], "sum", "category",
            "SELECT category, amount, quantity FROM sales",
            new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["showLegend"] = JsonSerializer.SerializeToElement(false),
                },
            });
        var ai = new AiChartConfig
        {
            ChartType = "bar",
            Title = "Sales",
            XAxis = "category",
            YAxis = ["amount", "quantity"],
            StyleConfig = new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["showLabels"] = JsonSerializer.SerializeToElement(true),
                },
            },
        };

        var result = ChartRefineMerger.Apply(baseline, ai, "show value labels", Allowlist);

        result.StyleConfig!.Params.Should().ContainKey("showLegend");
        result.StyleConfig.Params.Should().ContainKey("showLabels");
    }
}
