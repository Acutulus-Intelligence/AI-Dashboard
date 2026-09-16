using Domain.Models;

namespace Application.Services;

/// <summary>
/// Rejects collection <see cref="DataQueryModel"/> payloads that invent columns,
/// operators, or aggregations not present in the uploaded file schema.
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
    {
        var columns = new HashSet<string>(
            columnNames.Where(c => !string.IsNullOrWhiteSpace(c)),
            StringComparer.OrdinalIgnoreCase);

        string? Unknown(string kind, string name) =>
            $"{kind} '{name}' is not in the schema. Allowed columns: {string.Join(", ", columns)}.";

        foreach (var filter in model.Filters)
        {
            if (!columns.Contains(filter.Column))
                return Unknown("Filter column", filter.Column);
            if (!ValidOperators.Contains(filter.Operator))
                return $"Unsupported filter operator '{filter.Operator}'.";
        }

        foreach (var group in model.GroupBy)
        {
            if (!columns.Contains(group))
                return Unknown("Group-by column", group);
        }

        foreach (var agg in model.Aggregations)
        {
            if (!columns.Contains(agg.Column))
                return Unknown("Aggregation column", agg.Column);
            if (!ValidFunctions.Contains(agg.Function))
                return $"Unsupported aggregation function '{agg.Function}'.";
        }

        foreach (var order in model.OrderBy)
        {
            if (!columns.Contains(order.Column))
                return Unknown("Order-by column", order.Column);
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
            if (!string.IsNullOrWhiteSpace(config.XAxis) && !columns.Contains(config.XAxis))
                return Unknown("xAxis", config.XAxis);

            if (!string.IsNullOrWhiteSpace(config.GroupBy) && !columns.Contains(config.GroupBy))
                return Unknown("groupBy", config.GroupBy);

            foreach (var axis in config.YAxis)
            {
                if (!string.IsNullOrWhiteSpace(axis) && !columns.Contains(axis))
                    return Unknown("yAxis", axis);
            }
        }

        return null;
    }
}
