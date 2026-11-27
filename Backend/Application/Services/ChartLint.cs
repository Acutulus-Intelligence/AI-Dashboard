using System.Text.Json;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Deterministic readability pass that runs after the query result is known.
/// It fills in legend/value-label defaults and, when the user did not name a
/// chart type, replaces types that the data cannot support well (for example a
/// pie with 20 slices or a line with two points).
///
/// It never changes SQL and never invents columns. Every change is reported in
/// <c>aiDebug.notes</c> so the UI can explain it.
/// </summary>
public static class ChartLint
{
    private const int MaxPieSlices = 8;
    private const int MaxLabelledRows = 12;
    private const int MaxLabelledSeries = 3;
    private const int LongLabelLength = 12;

    public static void Apply(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows,
        bool allowTypeChange,
        List<string>? notes = null)
    {
        var chartType = ChartCatalog.CanonicalId(config.ChartType);
        if (chartType is null) return;
        config.ChartType = chartType;

        var rowCount = rows.Count;

        if (chartType != "table" && !HasNumericSeries(config, rows))
        {
            ChangeType(
                config,
                "table",
                "raw",
                notes,
                rowCount == 0
                    ? "The query returned no rows; using a table."
                    : "No numeric values were returned for the selected measure; using a table.");
            return;
        }

        if (allowTypeChange)
        {
            if (chartType == "heatmap" && !IsUsableHeatmap(config, rows))
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "Heatmap needs two categorical columns with multiple values; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "pivot" && !IsUsablePivot(config, rows))
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "Pivot needs two different categorical columns; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "funnel" && (rowCount < 2 || HasNegativeMeasure(config, rows)))
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "Funnel needs at least two non-negative stages; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "waterfall" && (rowCount < 2 || AllMeasureValuesAreZero(config, rows)))
            {
                ChangeType(
                    config,
                    "table",
                    "raw",
                    notes,
                    "Waterfall needs at least two non-zero changes; using a table.");
                chartType = config.ChartType;
            }
            else if (chartType == "treemap" && (rowCount < 2 || HasNegativeMeasure(config, rows)))
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "Treemap needs at least two non-negative categories; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "map" && !IsUsableMap(config, rows))
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "The map had no place or coordinate column; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType is "pie" or "radial" && rowCount > MaxPieSlices)
            {
                // An explicit topN means the user already tuned the slice count.
                if (!HasNumberParam(config, "topN"))
                {
                    ChangeType(
                        config,
                        "bar",
                        "default",
                        notes,
                        $"{chartType} had {rowCount} slices (more than {MaxPieSlices}); using a bar chart.");
                    chartType = config.ChartType;
                }
            }
            else if (chartType is "line" or "area" && rowCount is > 0 and < 3)
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    $"{chartType} had only {rowCount} point(s); using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "combo" && config.YAxis.Count < 2)
            {
                ChangeType(
                    config,
                    "bar",
                    "default",
                    notes,
                    "Combo needs at least two measures; using a bar chart.");
                chartType = config.ChartType;
            }
            else if (chartType == "scatter" && rowCount < 3)
            {
                ChangeType(
                    config,
                    "table",
                    "raw",
                    notes,
                    $"Scatter had only {rowCount} row(s); using a table.");
                chartType = config.ChartType;
            }
        }

        if (config.YAxis.Count >= 2)
        {
            SetParamIfAbsent(config, "showLegend", true);
        }

        if (chartType is "pie" or "radial" && rowCount is >= 2 and <= MaxPieSlices)
        {
            SetParamIfAbsent(config, "showLegend", true);
        }

        if (rowCount is >= 2 and <= MaxLabelledRows
            && config.YAxis.Count <= MaxLabelledSeries
            && chartType is "bar" or "line" or "area" or "combo")
        {
            SetParamIfAbsent(config, "showLabels", true);
        }

        if (chartType == "bar"
            && config.StyleConfig?.Variant is null
            && LongestLabel(config, rows) > LongLabelLength)
        {
            config.StyleConfig ??= new ChartStyleConfig();
            config.StyleConfig.Variant = "horizontal";
            notes?.Add("Long category labels detected; switched bars to horizontal.");
        }

        ApplySmartFormatting(config, rows, notes);
    }

    private static readonly string[] CountHints =
    [
        "count", "counts", "antal", "quantity", "qty", "users", "orders", "visits", "sessions", "rows"
    ];

    private static readonly string[] PercentHints =
    [
        "percent", "percentage", "pct", "procent", "share", "andel", "rate", "ratio", "margin", "marginal",
        "conversion", "utilization", "utnyttjande"
    ];

    /// <summary>
    /// Deterministic display formatting: counts get whole numbers and percent
    /// measures get a % suffix when the values look like 0–100.
    /// </summary>
    private static void ApplySmartFormatting(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows,
        List<string>? notes)
    {
        var spec = ChartCatalog.Find(config.ChartType);
        if (spec is null || !spec.SupportsValueFormat) return;

        var measure = config.YAxis.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(measure)) return;

        config.StyleConfig ??= new ChartStyleConfig();

        if (config.StyleConfig.Decimals is null && HasHint(measure, CountHints))
        {
            config.StyleConfig.Decimals = 0;
            notes?.Add($"'{measure}' looks like a count; showing whole numbers.");
        }

        if (string.IsNullOrWhiteSpace(config.StyleConfig.ValueSuffix)
            && HasHint(measure, PercentHints)
            && MaxAbsMeasure(config, rows) > 1.5)
        {
            config.StyleConfig.ValueSuffix = "%";
            config.StyleConfig.Decimals ??= 1;
            notes?.Add($"'{measure}' looks like a percentage; adding a % suffix.");
        }
    }

    private static bool HasHint(string name, IEnumerable<string> hints)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return hints.Any(hint =>
            normalized == hint
            || normalized.StartsWith($"{hint}_", StringComparison.Ordinal)
            || normalized.EndsWith($"_{hint}", StringComparison.Ordinal)
            || normalized.Contains($"_{hint}_", StringComparison.Ordinal));
    }

    private static double MaxAbsMeasure(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var max = 0d;
        foreach (var axis in config.YAxis)
        {
            foreach (var row in rows.Take(200))
            {
                if (ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value))
                    max = Math.Max(max, Math.Abs(value));
            }
        }

        return max;
    }

    private static bool HasNumericSeries(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return false;
        return config.YAxis.Any(axis => ChartResultNormalizer.IsNumericColumn(rows, axis));
    }

    private static void SetParamIfAbsent(AiChartConfig config, string key, bool value)
    {
        var spec = ChartCatalog.Find(config.ChartType);
        if (spec is null || spec.Params.All(p => !p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            return;

        config.StyleConfig ??= new ChartStyleConfig();
        config.StyleConfig.Params ??= new Dictionary<string, JsonElement>();

        if (config.StyleConfig.Params.Keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
            return;

        config.StyleConfig.Params[key] = JsonSerializer.SerializeToElement(value);
    }

    private static ChartParamSpec? FindParam(string chartType, string key) =>
        ChartCatalog.Find(chartType)?.Params.FirstOrDefault(p =>
            p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static bool HasNumberParam(AiChartConfig config, string key)
    {
        var param = FindParam(config.ChartType, key);
        if (param is null || param.Kind != ChartParamKind.Number) return false;

        return config.StyleConfig?.Params?.Keys
            .Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool IsUsableHeatmap(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return false;
        if (string.IsNullOrWhiteSpace(config.XAxis) || string.IsNullOrWhiteSpace(config.GroupBy))
            return false;
        if (!rows[0].ContainsKey(config.XAxis) || !rows[0].ContainsKey(config.GroupBy))
            return false;
        if (config.XAxis.Equals(config.GroupBy, StringComparison.OrdinalIgnoreCase))
            return false;

        return DistinctValues(config.XAxis, rows) >= 2
            && DistinctValues(config.GroupBy, rows) >= 2;
    }

    private static bool IsUsablePivot(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return false;
        if (string.IsNullOrWhiteSpace(config.XAxis) || string.IsNullOrWhiteSpace(config.GroupBy))
            return false;
        if (!rows[0].ContainsKey(config.XAxis) || !rows[0].ContainsKey(config.GroupBy))
            return false;

        return !config.XAxis.Equals(config.GroupBy, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNegativeMeasure(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        foreach (var axis in config.YAxis)
        {
            foreach (var row in rows.Take(200))
            {
                if (ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value)
                    && value < 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AllMeasureValuesAreZero(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var sawValue = false;

        foreach (var axis in config.YAxis)
        {
            foreach (var row in rows.Take(200))
            {
                if (!ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value))
                    continue;

                sawValue = true;
                if (value != 0) return false;
            }
        }

        return sawValue;
    }

    private static bool IsUsableMap(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return true;
        if (rows[0].Keys.Any(IsCoordinateColumn)) return true;
        if (string.IsNullOrWhiteSpace(config.XAxis)) return true;

        // Country/region names are valid place labels; a numeric category axis
        // is the common failure mode when no geo column exists.
        return !ChartResultNormalizer.IsNumericColumn(rows, config.XAxis);
    }

    private static bool IsCoordinateColumn(string key)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return normalized is "lat" or "latitude" or "lon" or "lng" or "longitude"
            || normalized.EndsWith("_lat", StringComparison.Ordinal)
            || normalized.EndsWith("_latitude", StringComparison.Ordinal)
            || normalized.EndsWith("_lon", StringComparison.Ordinal)
            || normalized.EndsWith("_lng", StringComparison.Ordinal)
            || normalized.EndsWith("_longitude", StringComparison.Ordinal);
    }

    private static int DistinctValues(
        string column,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Take(200))
        {
            if (row.TryGetValue(column, out var value))
                values.Add(value?.ToString() ?? string.Empty);
        }

        return values.Count;
    }

    private static int LongestLabel(AiChartConfig config, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (string.IsNullOrWhiteSpace(config.XAxis)) return 0;

        var max = 0;
        foreach (var row in rows.Take(50))
        {
            if (row.TryGetValue(config.XAxis, out var value))
                max = Math.Max(max, value?.ToString()?.Length ?? 0);
        }

        return max;
    }

    private static void ChangeType(
        AiChartConfig config,
        string chartType,
        string variant,
        List<string>? notes,
        string reason)
    {
        var spec = ChartCatalog.Find(chartType);
        if (spec is null) return;
        if (config.ChartType.Equals(spec.Id, StringComparison.OrdinalIgnoreCase)) return;

        notes?.Add(reason);
        config.ChartType = spec.Id;
        config.StyleConfig ??= new ChartStyleConfig();
        config.StyleConfig.Variant =
            spec.Variants.FirstOrDefault(v => v.Id.Equals(variant, StringComparison.OrdinalIgnoreCase))?.Id
            ?? spec.Variants[0].Id;

        // Params and unsupported presentation fields belong to the old type.
        config.StyleConfig.Params = null;
        if (!spec.SupportsColors)
        {
            config.StyleConfig.Palette = null;
            config.StyleConfig.Colors = null;
            config.StyleConfig.ColorsByKey = null;
        }

        if (!spec.SupportsValueFormat)
        {
            config.StyleConfig.ValuePrefix = null;
            config.StyleConfig.ValueSuffix = null;
            config.StyleConfig.Decimals = null;
            config.StyleConfig.DecimalMode = null;
            config.StyleConfig.Notation = null;
        }
    }
}
