using Application.DTos.Response;
using Application.Services;
using Domain.Charts;
using FluentAssertions;

namespace Application.Tests;

public class ChartRecommenderTests
{
    private static ChartRecommendationResponse Recommend(
        params (string Name, string DataType, bool Nullable)[] columns) =>
        ChartRecommender.Recommend("table", columns);

    [Fact]
    public void Date_and_one_measure_ranks_line_first()
    {
        var result = Recommend(
            ("order_date", "timestamp", false),
            ("amount", "numeric", false));

        result.Profile.Times.Should().Equal("order_date");
        result.Profile.Measures.Should().Equal("amount");
        result.Recommendations[0].ChartType.Should().Be("line");
        result.Recommendations[0].Confidence.Should().Be("high");
        result.Recommendations.Should().Contain(r => r.ChartType == "table");
        result.Recommendations[0].Suggested.XAxis.Should().Be("order_date");
    }

    [Fact]
    public void Date_named_text_columns_are_recognised_as_time()
    {
        var result = Recommend(
            ("order_date", "string", false),
            ("amount", "number", false));

        result.Profile.Times.Should().Equal("order_date");
        result.Recommendations[0].ChartType.Should().Be("line");
    }

    [Fact]
    public void Date_and_several_measures_ranks_combo_first()
    {
        var result = Recommend(
            ("order_date", "date", false),
            ("revenue", "numeric", false),
            ("cost", "numeric", false));

        result.Recommendations[0].ChartType.Should().Be("combo");
        result.Recommendations[0].Variant.Should().Be("default");
        result.Recommendations[0].Suggested.YAxis.Should().Equal("revenue", "cost");
    }

    [Fact]
    public void Single_measure_without_categories_ranks_kpi_first()
    {
        var result = Recommend(("amount", "numeric", false));

        result.Recommendations[0].ChartType.Should().Be("kpi");
        result.Recommendations[0].Suggested.XAxis.Should().BeNull();
        result.Recommendations[0].Suggested.YAxis.Should().Equal("amount");
    }

    [Fact]
    public void Country_and_measure_ranks_choropleth_map_first()
    {
        var result = Recommend(
            ("country", "text", false),
            ("revenue", "numeric", false));

        result.Profile.Geos.Should().Contain("country");
        result.Recommendations[0].ChartType.Should().Be("map");
        result.Recommendations[0].Variant.Should().Be("choropleth");
    }

    [Fact]
    public void Latitude_and_longitude_rank_marker_map_first()
    {
        var result = Recommend(
            ("latitude", "numeric", false),
            ("longitude", "numeric", false),
            ("revenue", "numeric", false));

        result.Recommendations[0].ChartType.Should().Be("map");
        result.Recommendations[0].Variant.Should().Be("markers");
    }

    [Fact]
    public void Low_cardinality_share_measure_ranks_pie_above_bar()
    {
        var result = Recommend(
            ("status", "text", false),
            ("percent_share", "numeric", false));

        var types = result.Recommendations.Select(r => r.ChartType).ToList();
        types.Should().Contain("pie");
        types.IndexOf("pie").Should().BeLessThan(types.IndexOf("bar"));
    }

    [Fact]
    public void Low_cardinality_category_without_share_still_ranks_bar_first()
    {
        var result = Recommend(
            ("status", "text", false),
            ("amount", "numeric", false));

        result.Recommendations[0].ChartType.Should().Be("bar");
        result.Recommendations.Should().Contain(r => r.ChartType == "pie");
    }

    [Fact]
    public void No_numeric_columns_falls_back_to_table_only()
    {
        var result = Recommend(
            ("name", "text", false),
            ("status", "text", true));

        result.Profile.Measures.Should().BeEmpty();
        result.Recommendations.Should().OnlyContain(r => r.ChartType == "table");
    }

    [Fact]
    public void Identifier_columns_are_not_treated_as_categories()
    {
        var result = Recommend(
            ("customer_id", "uuid", false),
            ("amount", "numeric", false));

        result.Profile.Dimensions.Should().BeEmpty();
        result.Recommendations[0].ChartType.Should().Be("kpi");
    }

    [Fact]
    public void Recommendations_are_ranked_and_only_use_catalog_types()
    {
        var result = Recommend(
            ("order_date", "timestamp", false),
            ("region", "text", false),
            ("amount", "numeric", false));

        result.Recommendations.Select(r => r.Score).Should().BeInDescendingOrder();
        result.Recommendations.Should().OnlyContain(r => ChartCatalog.IsKnownType(r.ChartType));
        result.Recommendations.Count.Should().BeLessThanOrEqualTo(4);
        result.Recommendations.Should().Contain(r => r.ChartType == "table");
    }

    [Fact]
    public void Two_categories_and_one_measure_rank_heatmap_first()
    {
        var result = Recommend(
            ("channel", "text", false),
            ("segment", "text", false),
            ("amount", "numeric", false));

        result.Recommendations[0].ChartType.Should().Be("heatmap");
        result.Recommendations[0].Suggested.XAxis.Should().Be("channel");
        result.Recommendations[0].Suggested.GroupBy.Should().Be("segment");
    }

    [Fact]
    public void Two_measures_without_a_date_rank_bar_before_scatter()
    {
        var result = Recommend(
            ("region", "text", false),
            ("revenue", "numeric", false),
            ("cost", "numeric", false));

        var types = result.Recommendations.Select(r => r.ChartType).ToList();
        types.IndexOf("bar").Should().BeLessThan(types.IndexOf("scatter"));
    }
}
