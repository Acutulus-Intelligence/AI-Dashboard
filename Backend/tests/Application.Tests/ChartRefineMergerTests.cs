using Application.Services;
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
    public void ClampColorsToAllowlist_maps_colour_index_labels()
    {
        var input = new List<string> { "Colour 2", "color_3" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().Equal("var(--chart-2)", "var(--chart-3)");
    }

    [Fact]
    public void ClampColorsToAllowlist_maps_hue_words_to_theme_tokens()
    {
        var input = new List<string> { "blue", "orange" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().Equal("var(--chart-1)", "var(--chart-2)");
    }

    [Fact]
    public void ClampColorsToAllowlist_snaps_near_hex_to_allowlist_hex()
    {
        var allow = new[] { "#3b82f6", "#ef4444" };
        var input = new List<string> { "#3b82f8" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, allow);

        result.Should().Equal("#3b82f6");
    }

    [Fact]
    public void SnapColorToAllowlist_maps_invented_blue_hex_to_chart_token()
    {
        ChartRefineMerger.SnapColorToAllowlist("#3b82f6", Allowlist).Should().Be("var(--chart-1)");
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
}
