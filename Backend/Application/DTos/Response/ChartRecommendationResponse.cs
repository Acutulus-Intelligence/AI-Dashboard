namespace Application.DTos.Response;

/// <summary>
/// Deterministic data profile used to rank chart types. Derived from schema
/// metadata only — no row data is read or sent anywhere.
/// </summary>
public sealed record ChartRecommendationProfile(
    string RowKind,
    IReadOnlyList<ChartProfileColumn> Columns,
    IReadOnlyList<string> Measures,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Times,
    IReadOnlyList<string> Geos);

/// <summary>One schema column with the role the recommender assigned to it.</summary>
public sealed record ChartProfileColumn(
    string Name,
    string Role,
    string DataType,
    bool Nullable);

/// <summary>A ranked chart-type suggestion with the reasoning the UI can show.</summary>
public sealed record ChartSuggestion(
    string ChartType,
    string Variant,
    int Score,
    string Confidence,
    IReadOnlyList<string> Reasons,
    ChartAxisSuggestion Suggested);

/// <summary>Column names the generator should prefer for this chart type.</summary>
public sealed record ChartAxisSuggestion(
    string? XAxis,
    IReadOnlyList<string> YAxis,
    string? GroupBy = null);

public sealed record ChartRecommendationResponse(
    ChartRecommendationProfile Profile,
    IReadOnlyList<ChartSuggestion> Recommendations);
