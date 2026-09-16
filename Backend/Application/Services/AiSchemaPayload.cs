using System.Text.Json;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Schema JSON sent to the model: metadata only, with an explicit identifier allowlist
/// so generation is grounded on the connected table rather than invented names.
/// </summary>
public static class AiSchemaPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(TableSchema schema)
    {
        var columnNames = schema.Columns.Select(c => c.ColumnName).ToList();
        return JsonSerializer.Serialize(new
        {
            table = schema.TableName,
            allowedTableName = schema.TableName,
            allowedColumnNames = columnNames,
            columns = schema.Columns.Select(c => new
            {
                name = c.ColumnName,
                type = c.DataType,
                nullable = c.IsNullable
            }),
            groundingRules = GroundingRules,
        }, JsonOptions);
    }

    public static string SerializeColumns(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string>? columnTypes = null)
    {
        var columns = columnNames.Select((name, i) => new
        {
            name,
            type = columnTypes is not null && i < columnTypes.Count ? columnTypes[i] : "unknown",
            nullable = true
        });

        return JsonSerializer.Serialize(new
        {
            table = tableName,
            allowedTableName = tableName,
            allowedColumnNames = columnNames,
            columns,
            groundingRules = GroundingRules,
        }, JsonOptions);
    }

    private static readonly string[] GroundingRules =
    [
        "Use ONLY allowedTableName and allowedColumnNames. Never invent tables or columns.",
        "FROM/JOIN must use allowedTableName (aliases of that table are allowed).",
        "xAxis, yAxis, groupBy, filters, and aggregations must use allowed columns or SELECT aliases of those columns.",
        "If the request cannot be answered from this schema, use the closest allowed columns and reflect that in the title — do not fabricate fields.",
    ];
}
