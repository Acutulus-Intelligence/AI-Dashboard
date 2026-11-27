using System.Globalization;
using System.Text.Json;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Reconciles chart axes with the columns the query actually returned. The AI
/// grounding step validates against the schema and SELECT aliases; this step
/// catches everything else (aliases the model got wrong, non-numeric measures,
/// or a query that returned no usable numeric column).
/// </summary>
public static class ChartResultNormalizer
{
    private const int SampleSize = 50;

    /// <summary>
    /// Mutates <paramref name="config"/> so its axes name real result columns.
    /// Returns false when the result has no columns at all.
    /// </summary>
    public static bool Normalize(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows,
        List<string>? notes = null)
    {
        var keys = CollectKeys(rows);
        if (keys.Count == 0) return false;

        var chartType = ChartCatalog.CanonicalId(config.ChartType) ?? config.ChartType;
        if (chartType == "table")
            return true;

        var xAxis = ResolveKey(keys, config.XAxis);
        if (xAxis is null && !string.IsNullOrWhiteSpace(config.XAxis))
        {
            notes?.Add($"xAxis '{config.XAxis}' was not returned by the query.");
        }

        xAxis ??= FirstCategoricalKey(rows, keys) ?? keys[0];

        if (!string.Equals(config.XAxis, xAxis, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(config.XAxis))
            {
                notes?.Add($"xAxis '{config.XAxis}' mapped to result column '{xAxis}'.");
            }

            config.XAxis = xAxis;
        }

        if (!string.IsNullOrWhiteSpace(config.GroupBy))
        {
            var groupBy = ResolveKey(keys, config.GroupBy);
            if (groupBy is null)
            {
                notes?.Add($"groupBy '{config.GroupBy}' was not returned by the query and was cleared.");
                config.GroupBy = null;
            }
            else
            {
                config.GroupBy = groupBy;
            }
        }

        var yAxis = new List<string>();
        foreach (var axis in config.YAxis)
        {
            var resolved = ResolveKey(keys, axis);
            if (resolved is null)
            {
                if (!string.IsNullOrWhiteSpace(axis))
                    notes?.Add($"yAxis '{axis}' was not returned by the query and was dropped.");
                continue;
            }

            if (!IsNumericColumn(rows, resolved))
            {
                notes?.Add($"yAxis '{resolved}' has no numeric values and was dropped.");
                continue;
            }

            if (!yAxis.Contains(resolved, StringComparer.Ordinal))
                yAxis.Add(resolved);
        }

        if (yAxis.Count == 0)
        {
            var fallback = keys.FirstOrDefault(k =>
                !IsIdentifierColumn(k) && IsNumericColumn(rows, k));
            if (fallback is null)
            {
                notes?.Add("No numeric column was returned; falling back to a table.");
                config.YAxis = yAxis;
                config.ChartType = "table";
                return true;
            }

            notes?.Add($"No valid yAxis remained; falling back to numeric column '{fallback}'.");
            yAxis.Add(fallback);
        }

        config.YAxis = yAxis;
        return true;
    }

    private static List<string> CollectKeys(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var keys = new List<string>();
        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    keys.Add(key);
            }
        }

        return keys;
    }

    private static string? ResolveKey(IReadOnlyList<string> keys, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return keys.FirstOrDefault(k => k.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstCategoricalKey(
        IReadOnlyList<Dictionary<string, object?>> rows,
        IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (!IsNumericColumn(rows, key) && HasValue(rows, key))
                return key;
        }

        return keys.FirstOrDefault();
    }

    private static bool HasValue(IReadOnlyList<Dictionary<string, object?>> rows, string key) =>
        rows.Take(SampleSize).Any(r => r.TryGetValue(key, out var value) && !IsNullValue(value));

    /// <summary>
    /// A missing value. Rows that round-trip through the chart result cache are
    /// deserialized by System.Text.Json, which represents SQL NULL as a
    /// JsonElement with ValueKind.Null instead of a CLR null.
    /// </summary>
    public static bool IsNullValue(object? value) =>
        value is null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    public static bool IsNumericColumn(IReadOnlyList<Dictionary<string, object?>> rows, string key)
    {
        var sampled = 0;
        var numeric = 0;

        foreach (var row in rows.Take(SampleSize))
        {
            if (!row.TryGetValue(key, out var value) || IsNullValue(value))
                continue;

            sampled++;
            if (IsNumber(value))
                numeric++;
        }

        // A single numeric-looking value is not enough: alphanumeric ID columns
        // often contain numbers in a few rows. Require a strict majority.
        return sampled > 0 && numeric > sampled / 2;
    }

    /// <summary>
    /// Identifier-like columns are never meaningful measures, even when every
    /// sampled value happens to be numeric.
    /// </summary>
    public static bool IsIdentifierColumn(string key)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized is "id" or "uuid" or "guid"
            || normalized.EndsWith("_id", StringComparison.Ordinal)
            || normalized.EndsWith("_uuid", StringComparison.Ordinal)
            || normalized.EndsWith("_guid", StringComparison.Ordinal);
    }

    private static bool IsNumber(object? value) => TryGetNumber(value, out _);

    /// <summary>Numeric result value as a double, for sorting and aggregation.</summary>
    public static bool TryGetNumber(object? value, out double number)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal:
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return !double.IsNaN(number) && !double.IsInfinity(number);

            // Cached chart rows come back from System.Text.Json as JsonElement
            // values, not CLR primitives. Treat them like their native types so
            // a cached numeric measure is not dropped as non-numeric.
            case JsonElement { ValueKind: JsonValueKind.Number } element:
                if (element.TryGetDecimal(out var decimalNumber))
                {
                    number = (double)decimalNumber;
                    return true;
                }

                if (element.TryGetDouble(out var doubleNumber)
                    && !double.IsNaN(doubleNumber)
                    && !double.IsInfinity(doubleNumber))
                {
                    number = doubleNumber;
                    return true;
                }

                number = 0;
                return false;

            case JsonElement { ValueKind: JsonValueKind.String } element:
                return TryGetNumber(element.GetString(), out number);

            case string text when !string.IsNullOrWhiteSpace(text)
                && decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                number = (double)parsed;
                return true;

            default:
                number = 0;
                return false;
        }
    }
}
