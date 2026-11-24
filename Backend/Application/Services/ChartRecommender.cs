using Application.DTos.Response;
using Domain.Charts;

namespace Application.Services;

/// <summary>
/// Deterministic chart-type recommender. Runs on schema metadata only: column
/// names, data types and nullability. No rows are read and nothing is sent to
/// an AI provider, so it is cheap enough to call before every generation.
///
/// Rules are ordered by score; each suggestion carries the reason the UI shows.
/// </summary>
public static class ChartRecommender
{
    private static readonly string[] LowCardinalityHints =
    [
        "status", "type", "typ", "category", "kategori", "segment", "group", "grupp",
        "region", "country", "land", "gender", "kon", "priority", "prioritet",
        "level", "niva", "flag", "boolean", "month", "manad", "quarter", "kvartal",
        "year", "ar", "class", "klass", "channel", "kanal"
    ];

    private static readonly string[] ShareHints =
    [
        "share", "percent", "procent", "andel", "rate", "ratio", "quota", "kvot"
    ];

    private static readonly string[] GeoHints =
    [
        "country", "region", "province", "county", "city", "stad", "kommun", "ort",
        "postcode", "postal", "zip", "state"
    ];

    public static ChartRecommendationResponse Recommend(
        string rowKind,
        IEnumerable<(string Name, string DataType, bool Nullable)> columns)
    {
        var profiled = columns
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new ChartProfileColumn(
                c.Name.Trim(),
                ResolveRole(c.Name, c.DataType),
                string.IsNullOrWhiteSpace(c.DataType) ? "unknown" : c.DataType,
                c.Nullable))
            .ToList();

        var measures = profiled.Where(c => c.Role == "measure").Select(c => c.Name).ToList();
        var geos = profiled.Where(c => c.Role == "geo").Select(c => c.Name).ToList();
        var times = profiled.Where(c => c.Role == "time").Select(c => c.Name).ToList();
        var dimensions = profiled
            .Where(c => c.Role is "dimension" or "geo")
            .Select(c => c.Name)
            .ToList();

        var profile = new ChartRecommendationProfile(
            rowKind, profiled, measures, dimensions, times, geos);

        var lowCardinality = profiled.Any(c =>
            c.Role == "dimension" && MatchesAny(c.Name, LowCardinalityHints));
        var shareMeasure = measures.Any(m => MatchesAny(m, ShareHints));
        var hasCoordinates = geos.Any(IsLatitude) && geos.Any(IsLongitude);

        var suggestions = new List<ChartSuggestion>();

        void Add(string chartType, string variant, int score, params string[] reasons)
        {
            var spec = ChartCatalog.Find(chartType);
            if (spec is null) return;
            var canonicalVariant = spec.Variants
                .FirstOrDefault(v => v.Id.Equals(variant, StringComparison.OrdinalIgnoreCase))?.Id
                ?? spec.Variants[0].Id;

            suggestions.Add(new ChartSuggestion(
                spec.Id,
                canonicalVariant,
                score,
                Confidence(score),
                reasons,
                SuggestAxes(spec.Id, measures, dimensions, times, geos, hasCoordinates)));
        }

        if (measures.Count == 1 && dimensions.Count == 0 && times.Count == 0)
        {
            Add("kpi", "sparkline", 95,
                "Exactly one numeric measure without categories or dates — a single headline value fits.");
        }

        if (times.Count > 0 && measures.Count >= 2)
        {
            Add("combo", "default", 92,
                "A date/time column plus several measures — bars with one line shows both volume and trend.");
        }

        if (times.Count > 0 && measures.Count >= 1)
        {
            Add("line", "default", 90,
                "A date/time column and a numeric measure — a line shows how the value changes over time.");
        }

        if (geos.Count > 0 && measures.Count >= 1)
        {
            Add("map", hasCoordinates ? "markers" : "choropleth", 88,
                hasCoordinates
                    ? "Latitude and longitude columns were detected alongside a numeric measure."
                    : "A geographic column was detected; a numeric measure can be shaded per place.");
        }

        if (dimensions.Count >= 1 && measures.Count == 1 && lowCardinality)
        {
            Add("pie", "donut", shareMeasure ? 82 : 76,
                shareMeasure
                    ? "A low-cardinality category and a share-like measure — parts of a whole."
                    : "A low-cardinality category with one measure — a donut shows the split.");
        }

        if (dimensions.Count >= 2 && measures.Count == 1)
        {
            Add("heatmap", "default", 80,
                "Two categorical columns and one measure — a heatmap shows their combination.");
        }

        if (dimensions.Count >= 1 && measures.Count >= 1)
        {
            Add("bar", "default", 78,
                "A category column and a numeric measure — bars compare values across categories.");
        }

        if (measures.Count >= 2 && times.Count == 0 && dimensions.Count <= 1)
        {
            Add("scatter", "default", 70,
                "Two numeric measures without a date — points reveal spread and correlation.");
        }

        if (measures.Count is >= 3 and <= 8 && times.Count == 0)
        {
            Add("radar", "default", 60,
                "Several numeric measures without a date — a radar compares their profiles.");
        }

        var tableReasons = measures.Count == 0
            ? new[] { "No numeric measure was detected — show the rows as a table." }
            : new[] { "Always available as a readable fallback for any result." };
        Add("table", "raw", 55, tableReasons);

        var ranked = suggestions
            .OrderByDescending(s => s.Score)
            .Take(3)
            .ToList();

        if (ranked.All(s => s.ChartType != "table"))
        {
            ranked.Add(suggestions.First(s => s.ChartType == "table"));
        }

        return new ChartRecommendationResponse(profile, ranked);
    }

    /// <summary>Maps a column onto measure/dimension/time/geo/identifier.</summary>
    public static string ResolveRole(string name, string dataType)
    {
        var hint = ChartGenerationSchema.UsageHint(dataType, name);
        var normalized = Normalize(name);

        // Identifiers are never plotted as a category, even when named like a place.
        if (hint == "identifier") return "identifier";

        if (hint == "coordinate" || IsLatitude(normalized) || IsLongitude(normalized)) return "geo";
        if (GeoHints.Any(h => normalized.Contains(h, StringComparison.Ordinal))) return "geo";
        if (hint == "dimension" && LooksLikeTimeName(normalized)) return "time";

        return hint;
    }

    private static bool LooksLikeTimeName(string normalized) =>
        normalized is "date" or "datum" or "month" or "manad" or "year" or "ar"
            or "quarter" or "kvartal" or "week" or "vecka" or "timestamp"
        || normalized.EndsWith("_date", StringComparison.Ordinal)
        || normalized.EndsWith("_datum", StringComparison.Ordinal)
        || normalized.EndsWith("_at", StringComparison.Ordinal)
        || normalized.StartsWith("date_", StringComparison.Ordinal)
        || normalized.StartsWith("datum_", StringComparison.Ordinal);

    private static ChartAxisSuggestion SuggestAxes(
        string chartType,
        IReadOnlyList<string> measures,
        IReadOnlyList<string> dimensions,
        IReadOnlyList<string> times,
        IReadOnlyList<string> geos,
        bool hasCoordinates)
    {
        var firstMeasure = measures.FirstOrDefault();
        var firstCategory = dimensions.FirstOrDefault(d => !IsLatitude(d) && !IsLongitude(d));
        var firstDimension = firstCategory ?? dimensions.FirstOrDefault();
        var secondDimension = dimensions.FirstOrDefault(d =>
            !d.Equals(firstDimension, StringComparison.OrdinalIgnoreCase));
        var firstTime = times.FirstOrDefault();
        var firstGeo = hasCoordinates
            ? geos.FirstOrDefault(IsLatitude)
            : geos.FirstOrDefault();

        return chartType switch
        {
            "kpi" => new ChartAxisSuggestion(
                null,
                firstMeasure is null ? [] : [firstMeasure]),

            "line" or "area" => new ChartAxisSuggestion(
                firstTime ?? firstDimension,
                measures.Take(3).ToList()),

            "combo" => new ChartAxisSuggestion(
                firstTime ?? firstDimension,
                measures.Take(3).ToList()),

            "map" => new ChartAxisSuggestion(
                firstGeo,
                firstMeasure is null ? [] : [firstMeasure]),

            "pie" or "radial" => new ChartAxisSuggestion(
                firstDimension,
                firstMeasure is null ? [] : [firstMeasure]),

            "heatmap" => new ChartAxisSuggestion(
                firstDimension,
                firstMeasure is null ? [] : [firstMeasure],
                secondDimension),

            "scatter" => new ChartAxisSuggestion(
                firstMeasure,
                measures.Skip(1).Take(2).ToList()),

            "radar" => new ChartAxisSuggestion(
                firstDimension,
                measures.Take(6).ToList()),

            "bar" => new ChartAxisSuggestion(
                firstDimension ?? firstTime ?? firstGeo,
                measures.Take(3).ToList()),

            _ => new ChartAxisSuggestion(
                firstDimension ?? firstTime ?? firstGeo,
                measures.Take(3).ToList()),
        };
    }

    private static string Confidence(int score) => score switch
    {
        >= 85 => "high",
        >= 70 => "medium",
        _ => "low",
    };

    private static bool MatchesAny(string name, IEnumerable<string> hints)
    {
        var normalized = Normalize(name);
        return hints.Any(hint => normalized.Contains(hint, StringComparison.Ordinal));
    }

    private static bool IsLatitude(string name) =>
        Normalize(name) is "lat" or "latitude" || Normalize(name).EndsWith("_lat", StringComparison.Ordinal);

    private static bool IsLongitude(string name) =>
        Normalize(name) is "lon" or "lng" or "longitude"
        || Normalize(name).EndsWith("_lon", StringComparison.Ordinal)
        || Normalize(name).EndsWith("_lng", StringComparison.Ordinal);

    private static string Normalize(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
}
