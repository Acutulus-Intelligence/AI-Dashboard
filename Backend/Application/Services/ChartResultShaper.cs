using System.Text.Json;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Applies the user's <c>topN</c> presentation setting after the query has run:
/// keeps the N largest categories and groups the rest into a single "Other"
/// row. Summing the tail preserves the chart total, which keeps pie/donut
/// shares honest.
///
/// Runs for bar, pie and radial only and always returns a new list, so cached
/// result rows are never mutated.
/// </summary>
public static class ChartResultShaper
{
    public const string OtherLabel = "Other";

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bar",
        "pie",
        "radial",
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

        var measure = config.YAxis.FirstOrDefault(axis =>
            ChartResultNormalizer.IsNumericColumn(rows, axis));
        if (measure is null) return Copy(rows);

        var ordered = rows
            .Select((row, index) => (
                Row: row,
                Index: index,
                Value: ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(measure), out var value)
                    ? value
                    : (double?)null))
            .OrderByDescending(item => item.Value ?? double.MinValue)
            .ThenBy(item => item.Index)
            .ToList();

        var kept = ordered.Take(topN).Select(item => item.Row).ToList();
        var tail = ordered.Skip(topN).Select(item => item.Row).ToList();

        var other = rows[0].Keys.ToDictionary(key => key, _ => (object?)null);

        foreach (var axis in config.YAxis)
        {
            if (!other.ContainsKey(axis)) continue;

            double sum = 0;
            foreach (var row in tail)
            {
                if (ChartResultNormalizer.TryGetNumber(row.GetValueOrDefault(axis), out var value))
                    sum += value;
            }

            other[axis] = sum;
        }

        other[xAxis] = OtherLabel;
        kept.Add(other);

        notes?.Add(
            $"Kept the top {topN} categories by '{measure}' and grouped {tail.Count} into '{OtherLabel}'.");

        return kept;
    }

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
