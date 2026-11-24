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
    public void Kpi_keeps_only_its_own_params_and_combo_keeps_legend_params()
    {
        var kpiStyle = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["sparklineHeight"] = JsonSerializer.SerializeToElement(50),
                ["showLegend"] = JsonSerializer.SerializeToElement(true),
            },
        };

        var kpi = ChartStyleSanitizer.Sanitize(kpiStyle, "kpi");
        kpi.Should().NotBeNull();
        kpi!.Params.Should().ContainKey("sparklineHeight");
        kpi.Params.Should().NotContainKey("showLegend");

        var comboStyle = new ChartStyleConfig
        {
            Params = new Dictionary<string, JsonElement>
            {
                ["showLegend"] = JsonSerializer.SerializeToElement(true),
                ["showLabels"] = JsonSerializer.SerializeToElement(false),
                ["sparklineHeight"] = JsonSerializer.SerializeToElement(50),
            },
        };

        var combo = ChartStyleSanitizer.Sanitize(comboStyle, "combo");
        combo.Should().NotBeNull();
        combo!.Params.Should().ContainKey("showLegend");
        combo.Params.Should().ContainKey("showLabels");
        combo.Params.Should().NotContainKey("sparklineHeight");
    }

    [Fact]
    public void Top_n_and_bin_counts_are_clamped_to_catalog_limits()
    {
        var bar = ChartStyleSanitizer.Sanitize(
            new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["topN"] = JsonSerializer.SerializeToElement(500),
                },
            },
            "bar");
        bar!.Params!["topN"].GetInt32().Should().Be(100);

        var histogram = ChartStyleSanitizer.Sanitize(
            new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["bins"] = JsonSerializer.SerializeToElement(500),
                },
            },
            "histogram");
        histogram!.Params!["bins"].GetInt32().Should().Be(60);
    }

    [Fact]
    public void Top_n_is_dropped_for_types_that_do_not_support_it()
    {
        var result = ChartStyleSanitizer.Sanitize(
            new ChartStyleConfig
            {
                Params = new Dictionary<string, JsonElement>
                {
                    ["topN"] = JsonSerializer.SerializeToElement(10),
                },
            },
            "line");

        result?.Params.Should().BeNull();
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

    [Fact]
    public void Map_keeps_catalog_params_and_drops_invented_ones()
    {
        var style = new ChartStyleConfig
        {
            Variant = "GRID",
            Params = new Dictionary<string, JsonElement>
            {
                ["fillOpacity"] = JsonDocument.Parse("2").RootElement.Clone(),
                ["showGrid"] = JsonDocument.Parse("true").RootElement.Clone(),
            },
        };

        var result = ChartStyleSanitizer.Sanitize(style, "map");

        result.Should().NotBeNull();
        result!.Variant.Should().Be("grid");
        result.Params.Should().ContainKey("fillOpacity");
        result.Params!["fillOpacity"].GetDouble().Should().Be(1);
        result.Params.Should().NotContainKey("showGrid");
    }
}
