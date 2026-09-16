using Application.Interfaces;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Post-generation checks for SQL charts: SELECT-only, schema-grounded identifiers,
/// catalog chart type, and style allowlists. Failures are returned as repair hints.
/// </summary>
public static class GeneratedChartGrounding
{
    public static string? ValidateSqlChart(
        AiChartConfig config,
        TableSchema schema,
        ISqlValidator sqlValidator)
    {
        if (string.IsNullOrWhiteSpace(config.ChartType) || !ChartCatalog.IsKnownType(config.ChartType))
        {
            return $"Unsupported or missing chartType '{config.ChartType}'. " +
                   $"Allowed: {string.Join(", ", ChartCatalog.TypeIds)}.";
        }

        if (string.IsNullOrWhiteSpace(config.SqlQuery))
            return "sqlQuery is missing.";

        if (!sqlValidator.IsSelectOnly(config.SqlQuery, out var selectError))
            return selectError ?? "SQL query is not a safe SELECT.";

        var columnNames = schema.Columns.Select(c => c.ColumnName).ToList();
        if (!SqlSchemaGrounder.TryValidate(config.SqlQuery, schema.TableName, columnNames, out var groundError))
            return groundError;

        return ValidateAxes(config, columnNames, config.SqlQuery);
    }

    public static string? ValidateStyle(AiChartConfig config, IReadOnlyList<string>? allowedColors)
    {
        var style = config.StyleConfig;
        if (style is null) return null;

        if (!string.IsNullOrWhiteSpace(style.Palette) && !ChartCatalog.IsKnownPalette(style.Palette))
        {
            return $"Unknown palette '{style.Palette}'. Allowed: " +
                   string.Join(", ", ChartCatalog.Palettes.Select(p => p.Id)) + ".";
        }

        if (style.Colors is { Count: > 0 } && allowedColors is { Count: > 0 })
        {
            var unknown = style.Colors
                .Where(c => !string.IsNullOrWhiteSpace(c)
                            && allowedColors.All(a => !a.Equals(c.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Invalid colours are clamped later; still tell the model so a retry can fix them.
            if (unknown.Count > 0)
            {
                return $"styleConfig.colors contains values not in the account allowlist: {string.Join(", ", unknown)}. " +
                       $"Allowed: {string.Join(", ", allowedColors)}.";
            }
        }

        if (style.Variant is not null)
        {
            var spec = ChartCatalog.Find(config.ChartType);
            if (spec is not null
                && spec.Variants.All(v => !v.Id.Equals(style.Variant, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Unknown variant '{style.Variant}' for chartType '{config.ChartType}'. " +
                       $"Allowed: {string.Join(", ", spec.Variants.Select(v => v.Id))}.";
            }
        }

        return null;
    }

    private static string? ValidateAxes(AiChartConfig config, IReadOnlyList<string> columnNames, string sql)
    {
        var tokens = SqlSchemaGrounder.Tokenize(sql);
        var aliases = SqlSchemaGrounder.ExtractSelectAliases(tokens);
        var known = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases) known.Add(alias);

        bool Allowed(string? name) =>
            string.IsNullOrWhiteSpace(name) || known.Contains(name.Trim());

        if (!Allowed(config.XAxis))
        {
            return $"xAxis '{config.XAxis}' is not a schema column or SELECT alias. " +
                   $"Allowed columns: {string.Join(", ", columnNames)}.";
        }

        if (!Allowed(config.GroupBy))
        {
            return $"groupBy '{config.GroupBy}' is not a schema column or SELECT alias. " +
                   $"Allowed columns: {string.Join(", ", columnNames)}.";
        }

        foreach (var axis in config.YAxis)
        {
            if (!Allowed(axis))
            {
                return $"yAxis '{axis}' is not a schema column or SELECT alias. " +
                       $"Allowed columns: {string.Join(", ", columnNames)}.";
            }
        }

        return null;
    }
}
