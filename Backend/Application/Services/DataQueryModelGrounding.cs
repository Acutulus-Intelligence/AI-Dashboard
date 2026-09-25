using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Rejects collection <see cref="DataQueryModel"/> payloads that invent columns,
/// operators, or aggregations not present in the uploaded file schema.
/// Unique near-miss column names are rewritten onto the schema.
/// </summary>
public static class DataQueryModelGrounding
{
    private static readonly HashSet<string> ValidOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "gt", "gte", "lt", "lte", "contains", "in", "notin", "isnull", "isnotnull",
    };

    private static readonly HashSet<string> ValidFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "sum", "avg", "min", "max",
    };

    public static string? Validate(
        DataQueryModel model,
        IReadOnlyList<string> columnNames,
        AiChartConfig? config = null)
        => Ground(model, columnNames, config);

    public static string? Ground(
        DataQueryModel model,
        IReadOnlyList<string> columnNames,
        AiChartConfig? config = null)
    {
        var columns = new SchemaIdentifierSet(columnNames.Where(c => !string.IsNullOrWhiteSpace(c)));

        string? Unknown(string kind, string name) =>
            $"{kind} '{name}' is not in the schema. Allowed columns: {columns.FormatAllowed()}.";

        bool Resolve(ref string name, string kind, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = Unknown(kind, name);
                return false;
            }

            if (columns.TryResolve(name, out var canonical))
            {
                name = canonical;
                return true;
            }

            error = Unknown(kind, name);
            return false;
        }

        foreach (var filter in model.Filters)
        {
            var column = filter.Column;
            if (!Resolve(ref column, "Filter column", out var error))
                return error;
            filter.Column = column;
            if (!ValidOperators.Contains(filter.Operator))
                return $"Unsupported filter operator '{filter.Operator}'.";
        }

        for (var i = 0; i < model.GroupBy.Count; i++)
        {
            var group = model.GroupBy[i];
            if (!Resolve(ref group, "Group-by column", out var error))
                return error;
            model.GroupBy[i] = group;
        }

        foreach (var agg in model.Aggregations)
        {
            var column = agg.Column;
            if (!Resolve(ref column, "Aggregation column", out var error))
                return error;
            agg.Column = column;
            if (!ValidFunctions.Contains(agg.Function))
                return $"Unsupported aggregation function '{agg.Function}'.";
        }

        foreach (var order in model.OrderBy)
        {
            var column = order.Column;
            if (!Resolve(ref column, "Order-by column", out var error))
                return error;
            order.Column = column;
            if (!order.Direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                && !order.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            {
                return $"Unsupported sort direction '{order.Direction}'.";
            }
        }

        if (model.Limit is < 0 or > 100_000)
            return "Row limit is out of range (0–100000).";

        if (config is not null)
        {
            if (!string.IsNullOrWhiteSpace(config.ChartType))
            {
                var canonicalType = ChartCatalog.CanonicalId(config.ChartType);
                if (canonicalType is null)
                {
                    return $"Unsupported or missing chartType '{config.ChartType}'. " +
                           $"Allowed: {string.Join(", ", ChartCatalog.TypeIds)}.";
                }

                config.ChartType = canonicalType;
            }

            if (!string.IsNullOrWhiteSpace(config.XAxis))
            {
                var axis = config.XAxis;
                if (!Resolve(ref axis, "xAxis", out var error))
                    return error;
                config.XAxis = axis;
            }

            if (!string.IsNullOrWhiteSpace(config.GroupBy))
            {
                var group = config.GroupBy;
                if (!Resolve(ref group, "groupBy", out var error))
                    return error;
                config.GroupBy = group;
            }

            for (var i = 0; i < config.YAxis.Count; i++)
            {
                var axis = config.YAxis[i];
                if (string.IsNullOrWhiteSpace(axis)) continue;
                if (!Resolve(ref axis, "yAxis", out var error))
                    return error;
                config.YAxis[i] = axis;
            }
        }

        return null;
    }
}
