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
        var pieShaped = false;

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
                if (FindParam(config.ChartType, "topN") is { Kind: ChartParamKind.Number })
                {
                    if (SetNumberParamIfAbsent(config, "topN", MaxPieSlices))
                    {
                        pieShaped = true;
                        notes?.Add(
                            $"{chartType} had {rowCount} slices; keeping the top {MaxPieSlices} and grouping the rest into '{ChartResultShaper.OtherLabel}'.");
                    }
                }
                else
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

        if (chartType is "pie" or "radial"
            && (pieShaped || rowCount is >= 2 and <= MaxPieSlices))
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

    private static bool SetNumberParamIfAbsent(AiChartConfig config, string key, int value)
    {
        var param = FindParam(config.ChartType, key);
        if (param is null || param.Kind != ChartParamKind.Number) return false;

        config.StyleConfig ??= new ChartStyleConfig();
        config.StyleConfig.Params ??= new Dictionary<string, JsonElement>();

        if (config.StyleConfig.Params.Keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
            return false;

        config.StyleConfig.Params[param.Key] = JsonSerializer.SerializeToElement(value);
        return true;
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
        }

        if (!spec.SupportsValueFormat)
        {
            config.StyleConfig.ValuePrefix = null;
            config.StyleConfig.ValueSuffix = null;
            config.StyleConfig.Decimals = null;
            config.StyleConfig.DecimalMode = null;
        }
    }
}
