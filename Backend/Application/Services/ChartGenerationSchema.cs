using System.Text.Json;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Structured schema + style constraints sent to the model. Listing allowlists
/// explicitly (tables, columns, chart types, colours) reduces invented identifiers.
/// </summary>
public static class ChartGenerationSchema
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static readonly string[] Aggregations = ["sum", "avg", "count", "min", "max", "none"];

    public static string ToJson(TableSchema schema, IReadOnlyList<string>? allowedColors = null)
        => JsonSerializer.Serialize(BuildPayload(
            schema.TableName,
            schema.Columns.Select(c => (c.ColumnName, c.DataType, c.IsNullable)),
            allowedColors), JsonOptions);

    public static string ToJson(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<string>? allowedColors = null)
    {
        var columns = columnNames.Select((name, i) =>
        {
            var type = i < columnTypes.Count ? columnTypes[i] : "unknown";
            return (name, type, true);
        });
        return JsonSerializer.Serialize(BuildPayload(tableName, columns, allowedColors), JsonOptions);
    }

    private static object BuildPayload(
        string tableName,
        IEnumerable<(string Name, string Type, bool Nullable)> columns,
        IReadOnlyList<string>? allowedColors)
    {
        var columnList = columns.ToList();
        return new
        {
            table = tableName,
            allowedTables = new[] { tableName },
            columns = columnList.Select(c => new
            {
                name = c.Name,
                type = c.Type,
                nullable = c.Nullable,
            }),
            allowedColumnNames = columnList.Select(c => c.Name).ToList(),
            allowedChartTypes = ChartCatalog.TypeIds,
            allowedPalettes = ChartCatalog.Palettes.Select(p => p.Id).ToList(),
            allowedColors = allowedColors is { Count: > 0 } ? allowedColors : Array.Empty<string>(),
            allowedAggregations = Aggregations,
            rules = new[]
            {
                "Never invent tables or columns. Use only allowedTables and allowedColumnNames.",
                "SELECT aliases are allowed; every source column must be in allowedColumnNames.",
                "If the request cannot be answered from listed columns, use the closest listed columns — never fabricate a name.",
                "styleConfig.colors values must be copied exactly from allowedColors (or omitted).",
                "styleConfig.palette must be one of allowedPalettes.",
                "styleConfig.variant must be a catalog variant of the chosen chartType.",
                "Do not set styleConfig.params or customColors.",
            },
        };
    }
}
