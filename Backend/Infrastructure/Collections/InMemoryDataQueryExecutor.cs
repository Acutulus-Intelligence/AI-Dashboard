using System.Globalization;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.ExternalDb;
using Microsoft.Extensions.Options;

namespace Infrastructure.Collections;

/// <summary>
/// Evaluates a structured <see cref="DataQueryModel"/> over uploaded rows
/// entirely in memory (filters, grouping, aggregations, ordering, limit),
/// applying the same row/byte/time limits as the SQL executors.
/// </summary>
public class InMemoryDataQueryExecutor : IDataQueryExecutor
{
    private readonly ExternalDbSettings _settings;

    public InMemoryDataQueryExecutor(IOptions<ExternalDbSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<List<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        DataQueryModel model,
        CancellationToken ct = default)
    {
        var typed = new List<Row>(rows.Count);
        foreach (var raw in rows)
        {
            ct.ThrowIfCancellationRequested();
            typed.Add(new Row(columnNames, columnTypes, raw));
        }

        IEnumerable<Row> candidates = typed;
        foreach (var filter in model.Filters)
        {
            candidates = candidates.Where(r => r.Matches(filter));
        }

        var matched = candidates.ToList();

        var grouped = Group(matched, model, columnNames, ct);

        var output = grouped.Select(r => new Dictionary<string, object?>(r.Values)).ToList();

        foreach (var order in model.OrderBy)
        {
            output = ApplySort(output, order);
        }

        var limited = model.Limit.HasValue ? output.Take(Math.Max(model.Limit.Value, 0)) : output;
        var results = limited.ToList();

        var totalBytes = 0L;
        foreach (var row in results)
        {
            ct.ThrowIfCancellationRequested();
            if (results.Count > _settings.QueryMaxRows)
                throw new InvalidOperationException($"Query exceeded the maximum row limit of {_settings.QueryMaxRows}.");
            foreach (var (_, value) in row)
            {
                if (value is not null)
                    totalBytes += EstimateSize(value);
            }
            if (totalBytes > _settings.QueryMaxBytes)
                throw new InvalidOperationException($"The result exceeded the maximum size of {_settings.QueryMaxBytes} bytes.");
        }

        return Task.FromResult(results);
    }

    private List<Dictionary<string, object?>> ApplySort(
        List<Dictionary<string, object?>> rows,
        DataOrderBy order)
    {
        var descending = order.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return descending
            ? rows
                .OrderByDescending(r => CoerceComparable(order.Column, r))
                .ThenByDescending(r => r.GetValueOrDefault(order.Column)?.ToString() ?? string.Empty)
                .ToList()
            : rows
                .OrderBy(r => CoerceComparable(order.Column, r))
                .ThenBy(r => r.GetValueOrDefault(order.Column)?.ToString() ?? string.Empty)
                .ToList();
    }

    private List<Row> Group(
        List<Row> matched,
        DataQueryModel model,
        IReadOnlyList<string> columnNames,
        CancellationToken ct)
    {
        var groupColumns = model.GroupBy
            .Where(g => columnNames.Contains(g))
            .Distinct()
            .ToList();

        var aggregateColumns = model.Aggregations
            .Where(a => columnNames.Contains(a.Column) && IsSupportedFunction(a.Function))
            .ToList();

        // No grouping and no aggregations: return the matched rows unchanged.
        if (groupColumns.Count == 0 && aggregateColumns.Count == 0)
            return matched;

        string KeyOf(Row r) => string.Join(
            "\u001F", groupColumns.Select(gc => r.Get(gc)?.ToString() ?? string.Empty));

        var groups = new List<Row>();
        foreach (var group in matched.GroupBy(KeyOf))
        {
            ct.ThrowIfCancellationRequested();

            var outValues = new Dictionary<string, object?>();
            foreach (var gc in groupColumns)
                outValues[gc] = group.FirstOrDefault()?.Get(gc);

            var sourceValues = group.ToList();

            foreach (var agg in aggregateColumns)
            {
                var values = sourceValues.Select(r => r.Get(agg.Column)).ToList();
                outValues[agg.Column] = Aggregate(agg.Function, values);
            }

            groups.Add(new Row(outValues));
            if (groups.Count > _settings.QueryMaxRows)
                throw new InvalidOperationException($"Query exceeded the maximum row limit of {_settings.QueryMaxRows}.");
        }

        return groups;
    }

    private static bool IsSupportedFunction(string function) => function.ToLowerInvariant() switch
    {
        "count" or "sum" or "avg" or "min" or "max" => true,
        _ => false
    };

    private static object? Aggregate(string function, List<object?> values)
    {
        switch (function.ToLowerInvariant())
        {
            case "count":
                return values.Count(v => v is not null);
            case "sum":
            {
                double sum = 0;
                var saw = false;
                foreach (var v in values)
                {
                    if (v is not null)
                    {
                        sum += ToDouble(v);
                        saw = true;
                    }
                }
                return saw ? sum : null;
            }
            case "avg":
            {
                double sum = 0;
                var count = 0;
                foreach (var v in values)
                {
                    if (v is not null)
                    {
                        sum += ToDouble(v);
                        count++;
                    }
                }
                return count > 0 ? sum / count : null;
            }
            case "min":
            {
                double? min = null;
                foreach (var v in values)
                {
                    if (v is null) continue;
                    var d = ToDouble(v);
                    if (min is null || d < min) min = d;
                }
                return min;
            }
            case "max":
            {
                double? max = null;
                foreach (var v in values)
                {
                    if (v is null) continue;
                    var d = ToDouble(v);
                    if (max is null || d > max) max = d;
                }
                return max;
            }
            default:
                throw new InvalidOperationException($"Unsupported aggregation function '{function}'.");
        }
    }

    private sealed class Row
    {
        public Row(Dictionary<string, object?> values)
        {
            Values = values;
        }

        public Row(IReadOnlyList<string> columns, IReadOnlyList<string> types, IReadOnlyList<string> raw)
        {
            Values = new Dictionary<string, object?>();
            for (var i = 0; i < columns.Count; i++)
            {
                var value = i < raw.Count ? raw[i] : null;
                Values[columns[i]] = Convert(value, i < types.Count ? types[i] : "string");
            }
        }

        public Dictionary<string, object?> Values { get; }

        public object? Get(string column) => Values.TryGetValue(column, out var v) ? v : null;

        public bool Matches(DataFilter filter)
        {
            if (!Values.TryGetValue(filter.Column, out var value))
                return false;

            return filter.Operator.ToLowerInvariant() switch
            {
                "eq" => Equal(value, filter.Value),
                "neq" => !Equal(value, filter.Value),
                "gt" => Compare(value, filter.Value) > 0,
                "gte" => Compare(value, filter.Value) >= 0,
                "lt" => Compare(value, filter.Value) < 0,
                "lte" => Compare(value, filter.Value) <= 0,
                "contains" => value?.ToString()?.Contains(filter.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
                "in" => (filter.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(v => Equal(value, v)),
                "notin" => !(filter.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(v => Equal(value, v)),
                "isnull" => value is null,
                "isnotnull" => value is not null,
                _ => false
            };
        }

        private static bool Equal(object? value, string? other)
        {
            if (value is null)
                return string.IsNullOrEmpty(other);
            if (other is null)
                return false;
            if (value is long l && long.TryParse(other, NumberStyles.Any, CultureInfo.InvariantCulture, out var lo))
                return l == lo;
            if (value is double d && double.TryParse(other, NumberStyles.Any, CultureInfo.InvariantCulture, out var dO))
                return Math.Abs(d - dO) < 1e-12;
            if (value is bool b && bool.TryParse(other, out var bo))
                return b == bo;
            return string.Equals(value.ToString(), other, StringComparison.OrdinalIgnoreCase);
        }

        private static int Compare(object? value, string? other)
        {
            if (value is null || other is null)
                return string.CompareOrdinal(value?.ToString(), other);

            if (value is long l && long.TryParse(other, NumberStyles.Any, CultureInfo.InvariantCulture, out var lo))
                return l.CompareTo(lo);
            if (value is double d && double.TryParse(other, NumberStyles.Any, CultureInfo.InvariantCulture, out var dO))
                return d.CompareTo(dO);
            return string.CompareOrdinal(value.ToString(), other);
        }

        private static object? Convert(string? value, string type)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return type switch
            {
                "integer" when long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var l) => l,
                "number" when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
                "boolean" when bool.TryParse(value, out var b) => b,
                _ => value
            };
        }
    }

    private static double ToDouble(object value) => value switch
    {
        double d => d,
        long l => l,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        _ => 0
    };

    private static IComparable? CoerceComparable(string column, Dictionary<string, object?> row)
    {
        var value = row.GetValueOrDefault(column);
        if (value is null)
            return null;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (value is IComparable comparable)
            return comparable;
        return value.ToString();
    }

    private static long EstimateSize(object value) => value switch
    {
        string s => s.Length * 2L,
        byte[] bytes => bytes.LongLength,
        _ => 16L
    };
}