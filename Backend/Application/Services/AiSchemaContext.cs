using System.Text.Json;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Formats table metadata for the model and exposes identifier allowlists.
/// Metadata only — never row values.
/// </summary>
public static class AiSchemaContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<string> TableNames(TableSchema schema) =>
        string.IsNullOrWhiteSpace(schema.TableName) ? [] : [schema.TableName];

    public static IReadOnlyList<string> ColumnNames(TableSchema schema) =>
        [.. schema.Columns
            .Select(c => c.ColumnName)
            .Where(n => !string.IsNullOrWhiteSpace(n))];

    public static TableSchema FromColumns(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string>? columnTypes = null)
    {
        var columns = new List<ColumnSchema>(columnNames.Count);
        for (var i = 0; i < columnNames.Count; i++)
        {
            columns.Add(new ColumnSchema
            {
                ColumnName = columnNames[i],
                DataType = columnTypes is { Count: > 0 } && i < columnTypes.Count
                    ? columnTypes[i]
                    : "unknown",
                IsNullable = true,
            });
        }

        return new TableSchema { TableName = tableName, Columns = columns };
    }

    public static string ToPromptJson(TableSchema schema)
    {
        var payload = new
        {
            databaseObject = "single table",
            allowedTables = TableNames(schema),
            allowedColumns = ColumnNames(schema),
            table = schema.TableName,
            columns = schema.Columns.Select(c => new
            {
                name = c.ColumnName,
                type = c.DataType,
                nullable = c.IsNullable,
                usageHint = UsageHint(c.DataType, c.ColumnName),
            }),
            constraints = new[]
            {
                "Use ONLY allowedTables and allowedColumns.",
                "Do not invent tables, columns, schemas, or joins to objects that are not listed.",
                "Never send or request row data; this payload is metadata only.",
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string UsageHint(string dataType, string columnName)
    {
        var t = dataType.ToLowerInvariant();
        var n = columnName.ToLowerInvariant();

        if (n is "id" or "uuid" || n.EndsWith("_id", StringComparison.Ordinal) || t.Contains("uuid"))
            return "identifier";
        if (t.Contains("timestamp") || t.Contains("date") || t.Contains("time"))
            return "time";
        if (t.Contains("bool"))
            return "dimension";
        if (t.Contains("int") || t.Contains("num") || t.Contains("dec") || t.Contains("float")
            || t.Contains("double") || t.Contains("money") || t.Contains("real") || t.Contains("serial"))
            return "measure";

        return "dimension";
    }
}
