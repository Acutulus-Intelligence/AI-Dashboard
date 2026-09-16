using System.Text;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Rejects AI chart output that invents tables, columns, aggregations, or
/// collection query fields that are not in the inspected schema.
/// </summary>
public static class AiOutputGrounding
{
    private static readonly HashSet<string> Aggregations = new(StringComparer.OrdinalIgnoreCase)
    {
        "sum", "avg", "count", "min", "max", "none",
    };

    private static readonly HashSet<string> FilterOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "gt", "gte", "lt", "lte", "contains", "in", "notin", "isnull", "isnotnull",
    };

    private static readonly HashSet<string> AggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "sum", "avg", "min", "max",
    };

    public sealed record Result(IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    public static Result ValidateSqlChart(AiChartConfig config, TableSchema schema)
    {
        var errors = new List<string>();
        var allowedTables = new HashSet<string>(AiSchemaContext.TableNames(schema), StringComparer.OrdinalIgnoreCase);
        var allowedColumns = new HashSet<string>(AiSchemaContext.ColumnNames(schema), StringComparer.OrdinalIgnoreCase);
        var aliases = new HashSet<string>(
            AiSqlIdentifierScanner.ExtractSelectAliases(config.SqlQuery),
            StringComparer.OrdinalIgnoreCase);
        var relationAliases = new HashSet<string>(
            AiSqlIdentifierScanner.ExtractRelationAliases(config.SqlQuery),
            StringComparer.OrdinalIgnoreCase);
        var allowedFields = new HashSet<string>(allowedColumns, StringComparer.OrdinalIgnoreCase);
        allowedFields.UnionWith(aliases);

        foreach (var table in AiSqlIdentifierScanner.ExtractReferencedTables(config.SqlQuery))
        {
            if (!allowedTables.Contains(table))
                errors.Add($"SQL references unknown table '{table}'. Allowed tables: {Join(allowedTables)}.");
        }

        if (!string.IsNullOrWhiteSpace(config.SqlQuery)
            && allowedTables.Count > 0
            && AiSqlIdentifierScanner.ExtractReferencedTables(config.SqlQuery).Count == 0
            && !ContainsAllowedTable(config.SqlQuery, allowedTables))
        {
            errors.Add($"SQL must query the allowed table '{Join(allowedTables)}'.");
        }

        foreach (var column in AiSqlIdentifierScanner.ExtractLikelyColumnReferences(config.SqlQuery))
        {
            if (allowedTables.Contains(column)
                || relationAliases.Contains(column)
                || allowedFields.Contains(column))
                continue;
            errors.Add($"SQL references unknown column '{column}'. Allowed columns: {Join(allowedColumns)}.");
        }

        ValidateAxes(config, allowedFields, allowedColumns, errors);
        ValidateAggregation(config.Aggregation, errors);
        return new Result(errors);
    }

    public static Result ValidateCollectionChart(AiChartConfig config, IReadOnlyList<string> columnNames)
    {
        var errors = new List<string>();
        var allowed = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);

        if (config.DataModel is null)
        {
            errors.Add("dataModel is required for uploaded collections.");
            return new Result(errors);
        }

        errors.AddRange(ValidateDataModel(config.DataModel, columnNames));
        ValidateAxes(config, allowed, allowed, errors);
        ValidateAggregation(config.Aggregation, errors);
        return new Result(errors);
    }

    public static IReadOnlyList<string> ValidateDataModel(DataQueryModel model, IReadOnlyList<string> columnNames)
    {
        var allowed = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var filter in model.Filters)
        {
            if (!allowed.Contains(filter.Column))
                errors.Add($"Filter references unknown column '{filter.Column}'.");
            if (!FilterOperators.Contains(filter.Operator))
                errors.Add($"Unsupported filter operator '{filter.Operator}'.");
        }

        foreach (var group in model.GroupBy)
        {
            if (!allowed.Contains(group))
                errors.Add($"groupBy references unknown column '{group}'.");
        }

        foreach (var agg in model.Aggregations)
        {
            if (!allowed.Contains(agg.Column))
                errors.Add($"Aggregation references unknown column '{agg.Column}'.");
            if (!AggregateFunctions.Contains(agg.Function))
                errors.Add($"Unsupported aggregation function '{agg.Function}'.");
        }

        foreach (var order in model.OrderBy)
        {
            if (!allowed.Contains(order.Column))
                errors.Add($"orderBy references unknown column '{order.Column}'.");
            if (order.Direction is not ("asc" or "desc"))
                errors.Add($"Unsupported sort direction '{order.Direction}'.");
        }

        if (model.Limit is < 0 or > 100_000)
            errors.Add("Row limit is out of range.");

        return errors;
    }

    public static string BuildRepairSuffix(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> allowedTables,
        IReadOnlyList<string> allowedColumns)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("CORRECTION REQUIRED. Your previous JSON was rejected:");
        foreach (var error in errors)
            sb.AppendLine("- " + error);

        sb.AppendLine();
        sb.AppendLine("Allowed tables (ONLY): " + Join(allowedTables));
        sb.AppendLine("Allowed columns (ONLY): " + Join(allowedColumns));
        sb.AppendLine("Return a complete JSON object using ONLY those identifiers. Do not invent tables, columns, or style params.");
        return sb.ToString();
    }

    private static void ValidateAxes(
        AiChartConfig config,
        IReadOnlySet<string> allowedFields,
        IReadOnlySet<string> allowedColumns,
        List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(config.XAxis) && !allowedFields.Contains(config.XAxis))
            errors.Add($"xAxis '{config.XAxis}' is not a schema column or SQL alias.");

        foreach (var y in config.YAxis)
        {
            if (!string.IsNullOrWhiteSpace(y) && !allowedFields.Contains(y))
                errors.Add($"yAxis '{y}' is not a schema column or SQL alias.");
        }

        if (!string.IsNullOrWhiteSpace(config.GroupBy)
            && !allowedFields.Contains(config.GroupBy)
            && !allowedColumns.Contains(config.GroupBy))
        {
            errors.Add($"groupBy '{config.GroupBy}' is not in the schema.");
        }
    }

    private static void ValidateAggregation(string? aggregation, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(aggregation)) return;
        if (!Aggregations.Contains(aggregation))
            errors.Add($"Unsupported aggregation '{aggregation}'. Use sum, avg, count, min, max, or none.");
    }

    private static bool ContainsAllowedTable(string sql, IReadOnlySet<string> allowedTables)
    {
        var cleaned = AiSqlIdentifierScanner.StripCommentsAndStringLiterals(sql);
        foreach (var table in allowedTables)
        {
            if (cleaned.Contains(table, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string Join(IEnumerable<string> names) =>
        string.Join(", ", names.Where(n => !string.IsNullOrWhiteSpace(n)));
}
