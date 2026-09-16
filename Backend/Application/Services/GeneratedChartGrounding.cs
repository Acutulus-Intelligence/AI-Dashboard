using Application.Interfaces;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Post-generation checks for SQL charts: SELECT-only, schema-grounded identifiers,
/// catalog chart type, and style allowlists. Unique near-miss names are rewritten
/// onto the connected schema; remaining failures are returned as repair hints.
/// </summary>
public static class GeneratedChartGrounding
{
    public static string? ValidateSqlChart(
        AiChartConfig config,
        TableSchema schema,
        ISqlValidator sqlValidator)
        => GroundSqlChart(config, schema, sqlValidator);

    /// <summary>
    /// Grounds SQL and axes onto <paramref name="schema"/>, mutating
    /// <paramref name="config"/> when unique typos can be repaired.
    /// </summary>
    public static string? GroundSqlChart(
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
        if (!SqlSchemaGrounder.TryGround(
                config.SqlQuery, schema.TableName, columnNames, out var groundedSql, out var groundError))
        {
            return groundError;
        }

        config.SqlQuery = groundedSql;
        return GroundAxes(config, columnNames, groundedSql);
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
                            && ChartRefineMerger.SnapColorToAllowlist(c, allowedColors) is null)
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

    private static string? GroundAxes(AiChartConfig config, IReadOnlyList<string> columnNames, string sql)
    {
        var tokens = SqlSchemaGrounder.Tokenize(sql);
        var aliases = SqlSchemaGrounder.ExtractSelectAliases(tokens);
        var known = new SchemaIdentifierSet(columnNames.Concat(aliases));

        if (!string.IsNullOrWhiteSpace(config.XAxis))
        {
            if (!known.TryResolve(config.XAxis, out var xCanonical))
            {
                return $"xAxis '{config.XAxis}' is not a schema column or SELECT alias. " +
                       $"Allowed columns: {string.Join(", ", columnNames)}.";
            }

            config.XAxis = xCanonical;
        }

        if (!string.IsNullOrWhiteSpace(config.GroupBy))
        {
            if (!known.TryResolve(config.GroupBy, out var groupCanonical))
            {
                return $"groupBy '{config.GroupBy}' is not a schema column or SELECT alias. " +
                       $"Allowed columns: {string.Join(", ", columnNames)}.";
            }

            config.GroupBy = groupCanonical;
        }

        for (var i = 0; i < config.YAxis.Count; i++)
        {
            var axis = config.YAxis[i];
            if (string.IsNullOrWhiteSpace(axis)) continue;
            if (!known.TryResolve(axis, out var yCanonical))
            {
                return $"yAxis '{axis}' is not a schema column or SELECT alias. " +
                       $"Allowed columns: {string.Join(", ", columnNames)}.";
            }

            config.YAxis[i] = yCanonical;
        }

        return null;
    }
}
