using Application.Services;
using Domain.Charts;
using FluentAssertions;

namespace Application.Tests;

public class ChartCatalogTests
{
    [Fact]
    public void Kpi_and_combo_are_registered()
    {
        ChartCatalog.IsKnownType("kpi").Should().BeTrue();
        ChartCatalog.IsKnownType("combo").Should().BeTrue();
        ChartCatalog.TypeIds.Should().Contain(["kpi", "combo"]);
    }

    [Fact]
    public void Histogram_and_heatmap_are_registered()
    {
        ChartCatalog.IsKnownType("histogram").Should().BeTrue();
        ChartCatalog.IsKnownType("heatmap").Should().BeTrue();
        ChartCatalog.Find("histogram")!.Params.Should().Contain(p => p.Key == "bins");
        ChartCatalog.Find("heatmap")!.Params.Should().Contain(p => p.Key == "showLabels");
    }

    [Fact]
    public void Pivot_funnel_and_waterfall_are_registered()
    {
        ChartCatalog.IsKnownType("pivot").Should().BeTrue();
        ChartCatalog.IsKnownType("funnel").Should().BeTrue();
        ChartCatalog.IsKnownType("waterfall").Should().BeTrue();

        ChartCatalog.Find("pivot")!.SupportsColors.Should().BeFalse();
        ChartCatalog.Find("pivot")!.Params.Should().Contain(p => p.Key == "showTotals");
        ChartCatalog.Find("funnel")!.Params.Should().Contain(p => p.Key == "showPercent");
        ChartCatalog.Find("waterfall")!.Params.Should().Contain(p => p.Key == "showTotals");
    }

    [Fact]
    public void Top_n_is_available_on_bar_pie_and_radial()
    {
        foreach (var type in new[] { "bar", "pie", "radial" })
        {
            ChartCatalog.Find(type)!.Params.Should().Contain(
                p => p.Key == "topN" && p.Kind == ChartParamKind.Number,
                type);
        }
    }

    [Fact]
    public void Every_type_has_unique_variants_and_params()
    {
        foreach (var type in ChartCatalog.Types)
        {
            type.Variants.Should().NotBeEmpty(type.Id);
            type.Variants.Select(v => v.Id).Should().OnlyHaveUniqueItems(type.Id);
            type.Params.Select(p => p.Key).Should().OnlyHaveUniqueItems(type.Id);
        }
    }

    [Fact]
    public void Kpi_offers_value_sparkline_and_trend_variants()
    {
        var kpi = ChartCatalog.Find("kpi");

        kpi.Should().NotBeNull();
        kpi!.Variants.Select(v => v.Id).Should().Equal("value", "sparkline", "trend");
        kpi.Params.Should().Contain(p => p.Key == "sparklineHeight");
    }

    [Fact]
    public void Combo_has_legend_on_by_default_and_does_not_duplicate_line_thickness()
    {
        var combo = ChartCatalog.Find("combo");

        combo.Should().NotBeNull();
        combo!.Variants.Select(v => v.Id).Should().Equal("default", "lineFirst");
        combo.Params.Should().Contain(p => p.Key == "showLegend" && (bool)p.Default);
        combo.Params.Should().Contain(p => p.Key == "showLabels");
    }

    [Fact]
    public void Prompt_catalog_mentions_the_new_types_and_allowed_params()
    {
        var catalog = AiChartPromptBuilder.DescribeCatalog();

        catalog.Should().Contain("kpi:");
        catalog.Should().Contain("combo:");
        catalog.Should().Contain("histogram:");
        catalog.Should().Contain("heatmap:");
        catalog.Should().Contain("pivot:");
        catalog.Should().Contain("funnel:");
        catalog.Should().Contain("waterfall:");
        catalog.Should().Contain("showLegend");
        catalog.Should().Contain("showLabels");
    }
}
