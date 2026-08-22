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
    public void ClampColorsToAllowlist_uses_canonical_allowlist_casing()
    {
        var input = new List<string> { "VAR(--chart-1)" };

        var result = ChartRefineMerger.ClampColorsToAllowlist(input, Allowlist);

        result.Should().BeEquivalentTo(["var(--chart-1)"]);
    }
}
