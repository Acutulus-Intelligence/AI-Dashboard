using System.Text.Json;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Applies the user's <c>topN</c> presentation setting after the query has run:
/// keeps the N largest categories and groups the rest into a single "Other"
/// row. The tail uses the chart's aggregation (sum/avg/min/max) instead of
/// blindly summing, and an existing "Other" category is folded into the tail so
/// labels stay unique. Sorting matches the renderer (total across all measures).
///
/// Runs for bar, pie, radial and treemap only and always returns a new list, so
/// cached result rows are never mutated.
/// </summary>
public static class ChartResultShaper
{
    public const string OtherLabel = "Other";

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bar",
        "pie",
        "radial",
        "treemap",
    };

    public static List<Dictionary<string, object?>> Shape(
        AiChartConfig config,
        IReadOnlyList<Dictionary<string, object?>> rows,
        List<string>? notes = null)
    {
        var topN = ReadTopN(config);
        if (topN <= 0 || rows.Count <= topN) return Copy(rows);

        var chartType = ChartCatalog.CanonicalId(config.ChartType);
        if (chartType is null || !SupportedTypes.Contains(chartType)) return Copy(rows);

        if (string.IsNullOrWhiteSpace(config.XAxis)) return Copy(rows);
        var xAxis = config.XAxis;
        if (!rows[0].ContainsKey(xAxis)) return Copy(rows);

        var measureAxes = config.YAxis
            .Where(axis => !ChartResultNormalizer.IsIdentifierColumn(axis)
                && ChartResultNormalizer.IsNumericColumn(rows, axis))
            .ToList();
        if (measureAxes.Count == 0) return Copy(rows);

        var aggregation = NormalizeAggregation(config.Aggregation);

        // Grouping non-additive measures into "Other" would be misleading: an
        // unweighted average/min/max of category values is not the real total.
        if (aggregation is "avg" or "min" or "max" or "none")
        {
            notes?.Add("Top N requires an additive measure; showing all categories.");
            return Copy(rows);
        }

        var ordered = rows
            .Select((row, index) => (
                Row: row,
                Index: index,
                Score: RowScore(row, measureAxes),
                IsOther: string.Equals(
                    row.GetValueOrDefault(xAxis)?.ToString(), OtherLabel, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .ToList();

        // An existing "Other" row is always part of the tail; keeping it next to
        // the generated row would produce two identical labels.
        var kept = ordered
            .Where(item => !item.IsOther)
            .Take(topN)
            .Select(item => item.Row)
            .ToList();
        var keptSet = new HashSet<Dictionary<string, object?>>(kept);
        var tail = ordered
            .Where(item => item.IsOther || !keptSet.Contains(item.Row))
            .Select(item => item.Row)
            .ToList();

        var other = new Dictionary<string, object?>();
        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
                other.TryAdd(key, null);
        }

        foreach (var axis in config.YAxis)
        {
            if (!other.ContainsKey(axis)) continue;
            other[axis] = Aggregate(tail, axis, aggregation);
        }

        other[xAxis] = OtherLabel;
        kept.Add(other);

        notes?.Add(
            $"Kept the top {topN} categories and grouped {tail.Count} into '{OtherLabel}' using {aggregation}.");

        return kept;
    }

    private static double RowScore(
        Dictionary<string, object?> row,
        IReadOnlyList<string> measureAxes)
    {
        double score = 0;
        foreach (var axis in measureAxes)
        {
            if (ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value))
                score += value;
        }

        return score;
    }

    private static object? Aggregate(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string axis,
        string aggregation)
    {
        if (rows.Count == 0) return null;

        var values = new List<double>(rows.Count);
        foreach (var row in rows)
        {
            if (ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value))
                values.Add(value);
        }

        if (values.Count == 0) return null;

        return aggregation switch
        {
            "avg" => values.Average(),
            "min" => values.Min(),
            "max" => values.Max(),
            _ => values.Sum(),
        };
    }

    private static string NormalizeAggregation(string? aggregation) =>
        string.IsNullOrWhiteSpace(aggregation) ? "sum" : aggregation.Trim().ToLowerInvariant();

    private static List<Dictionary<string, object?>> Copy(
        IReadOnlyList<Dictionary<string, object?>> rows) => [.. rows];

    private static int ReadTopN(AiChartConfig config)
    {
        var spec = ChartCatalog.Find(config.ChartType);
        if (spec is null || spec.Params.All(p => p.Key != "topN")) return 0;

        if (config.StyleConfig?.Params is not { } parameters) return 0;

        var entry = parameters.FirstOrDefault(pair =>
            pair.Key.Equals("topN", StringComparison.OrdinalIgnoreCase));
        if (entry.Key is null || entry.Value.ValueKind != JsonValueKind.Number) return 0;
        if (!entry.Value.TryGetInt32(out var value)) return 0;

        return Math.Clamp(value, 0, 100);
    }
}
