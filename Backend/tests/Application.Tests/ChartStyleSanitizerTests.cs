using System.Text.Json;
using Application.Services;
using Domain.Charts;
using FluentAssertions;

namespace Application.Tests;

public class ChartStyleSanitizerTests
{
    [Fact]
    public void Drops_invented_variant_palette_params_and_unsafe_colors()
    {
        var style = new ChartStyleConfig
        {
            Variant = "spiral",
            Palette = "rainbow",
            Colors = ["not-a-color", "javascript:alert(1)"],
            Params = new Dictionary<string, JsonElement>
            {
                ["inventedParam"] = JsonDocument.Parse("true").RootElement.Clone(),
                ["showGrid"] = JsonDocument.Parse("true").RootElement.Clone(),
            },
        };

        var result = ChartStyleSanitizer.Sanitize(style, "bar");

        result.Should().NotBeNull();
        result!.Variant.Should().BeNull();
        result.Palette.Should().BeNull();
        result.Colors.Should().BeNull();
        result.Params.Should().ContainKey("showGrid");
        result.Params.Should().NotContainKey("inventedParam");
    }

    [Fact]
    public void TakeAiControlledStyleFields_clamps_invented_hex_to_account_allowlist()
    {
        var style = new ChartStyleConfig
        {
            Colors = ["#ff00aa", "var(--chart-1)"],
            Palette = "warm",
        };

        var result = ChartRefineMerger.TakeAiControlledStyleFields(
            style,
            "bar",
            ["var(--chart-1)", "var(--chart-2)"]);

        result.Should().NotBeNull();
        // Palette XOR: palette "warm" is valid so colours are dropped.
        result!.Palette.Should().Be("warm");
        result.Colors.Should().BeNull();
    }

    [Fact]
    public void TakeAiControlledStyleFields_keeps_only_allowlisted_series_colors()
    {
        var style = new ChartStyleConfig
        {
            Colors = ["#ff00aa", "var(--chart-2)"],
        };

        var result = ChartRefineMerger.TakeAiControlledStyleFields(
            style,
            "bar",
            ["var(--chart-1)", "var(--chart-2)"]);

        result.Should().NotBeNull();
        result!.Palette.Should().BeNull();
        result.Colors.Should().Equal("", "var(--chart-2)");
    }

    [Fact]
    public void Table_charts_cannot_keep_colours_or_value_format()
    {
        var style = new ChartStyleConfig
        {
            Palette = "cool",
            Colors = ["var(--chart-1)"],
            ValuePrefix = "$",
            Decimals = 2,
        };

        var result = ChartRefineMerger.TakeAiControlledStyleFields(
            style,
            "table",
            ["var(--chart-1)"]);

        result.Should().BeNull();
    }
}
