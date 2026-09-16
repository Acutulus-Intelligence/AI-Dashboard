using System.Text;
using System.Text.RegularExpressions;
using Application.DTos.Request;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Result of grounding an AI chart against the connected schema.
/// Local repairs (typos, case) are applied to <see cref="Config"/> when unique.
/// Remaining <see cref="Errors"/> should trigger a model repair pass or a hard fail.
/// </summary>
public sealed class AiGroundingResult
{
    public required AiChartConfig Config { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool IsGrounded => Errors.Count == 0;
}

/// <summary>
/// Validates and locally repairs AI SQL / axes / data-model identifiers so they
/// refer only to the connected table schema. Complements SELECT-only SQL checks
/// and catalog style sanitization.
/// </summary>
public static partial class AiOutputGrounding
{
    public const int MaxRepairAttempts = 1;

    public static AiGroundingResult GroundSqlChart(
        TableSchema schema,
        AiChartConfig config,
        ChartBaseline? baseline = null)
    {
        var notes = new List<string>();
        var errors = new List<string>();
        var columns = new IdentifierSet(schema.Columns.Select(c => c.ColumnName));
        var tableName = schema.TableName;

        if (string.IsNullOrWhiteSpace(tableName) || columns.Count == 0)
        {
            errors.Add("Connected schema is empty; cannot ground generated SQL.");
            return new AiGroundingResult { Config = config, Notes = notes, Errors = errors };
        }

        if (!string.IsNullOrWhiteSpace(config.SqlQuery))
        {
            var sqlResult = GroundSql(config.SqlQuery, tableName, columns);
            config.SqlQuery = sqlResult.Sql;
            notes.AddRange(sqlResult.Notes);

            var typeUnchanged = baseline is null
                || string.Equals(config.ChartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);

            if (sqlResult.Errors.Count > 0
                && baseline is not null
                && typeUnchanged
                && !string.IsNullOrWhiteSpace(baseline.SqlQuery))
            {
                var baselineSql = GroundSql(baseline.SqlQuery, tableName, columns);
                if (baselineSql.Errors.Count == 0)
                {
                    notes.Add(
                        $"AI SQL referenced unknown identifiers ({string.Join("; ", sqlResult.Errors)}); kept baseline SQL.");
                    config.SqlQuery = baselineSql.Sql;
                }
                else
                {
                    errors.AddRange(sqlResult.Errors);
                }
            }
            else
            {
                errors.AddRange(sqlResult.Errors);
            }
        }

        var aliases = ExtractSelectAliases(config.SqlQuery);
        var axisNames = columns.With(aliases);

        config.XAxis = GroundField(config.XAxis, axisNames, "xAxis", notes, errors, required: false);
        config.GroupBy = string.IsNullOrWhiteSpace(config.GroupBy)
            ? config.GroupBy
            : GroundField(config.GroupBy, axisNames, "groupBy", notes, errors, required: false);

        if (config.YAxis is { Count: > 0 })
        {
            var groundedY = new List<string>(config.YAxis.Count);
            for (var i = 0; i < config.YAxis.Count; i++)
            {
                var value = GroundField(config.YAxis[i], axisNames, $"yAxis[{i}]", notes, errors, required: false);
                if (!string.IsNullOrWhiteSpace(value))
                    groundedY.Add(value);
            }

            if (groundedY.Count > 0)
                config.YAxis = groundedY;
            else
                errors.Add("yAxis has no columns that exist in the schema or SQL aliases.");
        }

        return new AiGroundingResult { Config = config, Notes = notes, Errors = errors };
    }

    public static AiGroundingResult GroundCollectionChart(
        IReadOnlyList<string> columnNames,
        AiChartConfig config)
    {
        var notes = new List<string>();
        var errors = new List<string>();
        var columns = new IdentifierSet(columnNames);

        if (columns.Count == 0)
        {
            errors.Add("Data schema is empty; cannot ground generated chart.");
            return new AiGroundingResult { Config = config, Notes = notes, Errors = errors };
        }

        config.XAxis = GroundField(config.XAxis, columns, "xAxis", notes, errors, required: false);
        config.GroupBy = string.IsNullOrWhiteSpace(config.GroupBy)
            ? config.GroupBy
            : GroundField(config.GroupBy, columns, "groupBy", notes, errors, required: false);

        if (config.YAxis is { Count: > 0 })
        {
            var groundedY = new List<string>(config.YAxis.Count);
            for (var i = 0; i < config.YAxis.Count; i++)
            {
                var value = GroundField(config.YAxis[i], columns, $"yAxis[{i}]", notes, errors, required: false);
                if (!string.IsNullOrWhiteSpace(value))
                    groundedY.Add(value);
            }

            if (groundedY.Count > 0)
                config.YAxis = groundedY;
        }

        if (config.DataModel is not null)
            GroundDataModel(config.DataModel, columns, notes, errors);

        return new AiGroundingResult { Config = config, Notes = notes, Errors = errors };
    }

    public static string BuildRepairPrompt(AiGroundingResult grounding, string? previousJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Your previous JSON was rejected because it was not grounded in the connected schema.");
        foreach (var error in grounding.Errors)
            sb.AppendLine("- " + error);
        sb.AppendLine(
            "Regenerate the complete JSON object. Use ONLY allowedTableName and allowedColumnNames from the schema. Do not invent columns, tables, filters, or colour values.");
        AppendPreviousJson(sb, previousJson);
        return sb.ToString();
    }

    public static string BuildExecutionRepairPrompt(string executionError, string? previousJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("The SQL you generated failed when executed against the connected database:");
        sb.AppendLine(executionError.Length <= 500 ? executionError : executionError[..500] + "…");
        sb.AppendLine(
            "This usually means invented tables/columns. Regenerate valid SELECT SQL using ONLY allowedTableName and allowedColumnNames.");
        AppendPreviousJson(sb, previousJson);
        return sb.ToString();
    }

    public static bool IsLikelySchemaError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var m = message.ToLowerInvariant();
        return m.Contains("does not exist", StringComparison.Ordinal)
            || m.Contains("unknown column", StringComparison.Ordinal)
            || m.Contains("no such column", StringComparison.Ordinal)
            || m.Contains("invalid column", StringComparison.Ordinal)
            || m.Contains("undefined column", StringComparison.Ordinal)
            || m.Contains("no such table", StringComparison.Ordinal)
            || m.Contains("unknown table", StringComparison.Ordinal)
            || m.Contains("invalid object name", StringComparison.Ordinal)
            || m.Contains("invalid object", StringComparison.Ordinal)
            || (m.Contains("relation", StringComparison.Ordinal) && m.Contains("does not", StringComparison.Ordinal))
            || (m.Contains("column", StringComparison.Ordinal) && m.Contains("not exist", StringComparison.Ordinal))
            || (m.Contains("table", StringComparison.Ordinal) && m.Contains("not exist", StringComparison.Ordinal));
    }

    private static void AppendPreviousJson(StringBuilder sb, string? previousJson)
    {
        if (string.IsNullOrWhiteSpace(previousJson)) return;
        sb.AppendLine();
        sb.AppendLine("Previous JSON:");
        sb.AppendLine(previousJson.Length <= 2000 ? previousJson : previousJson[..2000] + "…");
    }

    private static void GroundDataModel(
        DataQueryModel model,
        IdentifierSet columns,
        List<string> notes,
        List<string> errors)
    {
        var validOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "eq", "neq", "gt", "gte", "lt", "lte", "contains", "in", "notin", "isnull", "isnotnull"
        };
        var validFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "count", "sum", "avg", "min", "max"
        };

        foreach (var filter in model.Filters)
        {
            filter.Column = GroundField(filter.Column, columns, "dataModel.filter.column", notes, errors, required: true);
            if (!validOperators.Contains(filter.Operator))
                errors.Add($"Unsupported filter operator '{filter.Operator}'.");
        }

        for (var i = 0; i < model.GroupBy.Count; i++)
            model.GroupBy[i] = GroundField(model.GroupBy[i], columns, "dataModel.groupBy", notes, errors, required: true);

        foreach (var agg in model.Aggregations)
        {
            agg.Column = GroundField(agg.Column, columns, "dataModel.aggregation.column", notes, errors, required: true);
            if (!validFunctions.Contains(agg.Function))
                errors.Add($"Unsupported aggregation function '{agg.Function}'.");
        }

        foreach (var order in model.OrderBy)
        {
            order.Column = GroundField(order.Column, columns, "dataModel.orderBy.column", notes, errors, required: true);
            if (order.Direction is not ("asc" or "desc"))
                errors.Add($"Unsupported sort direction '{order.Direction}'.");
        }

        if (model.Limit is < 0 or > 100_000)
            errors.Add("Row limit is out of range.");
    }

    private static string GroundField(
        string? value,
        IdentifierSet allowed,
        string fieldName,
        List<string> notes,
        List<string> errors,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
                errors.Add($"{fieldName} is empty.");
            return value ?? string.Empty;
        }

        var trimmed = Unquote(value.Trim());
        if (allowed.TryCanonical(trimmed, out var canonical))
        {
            if (!canonical.Equals(trimmed, StringComparison.Ordinal))
                notes.Add($"Rewrote {fieldName} '{trimmed}' to schema name '{canonical}'.");
            return canonical;
        }

        if (allowed.TryFuzzy(trimmed, out var fuzzy))
        {
            notes.Add($"Repaired {fieldName} '{trimmed}' to closest schema name '{fuzzy}'.");
            return fuzzy;
        }

        errors.Add($"{fieldName} '{trimmed}' is not in the schema. Allowed: {allowed.FormatAllowed()}.");
        return trimmed;
    }

    private static SqlGrounding GroundSql(string sql, string tableName, IdentifierSet columns)
    {
        var notes = new List<string>();
        var errors = new List<string>();
        var rewritten = sql;

        var tables = ExtractFromJoinTables(sql);
        var tableAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableSet = new IdentifierSet([tableName]);

        foreach (var table in tables)
        {
            if (tableSet.TryCanonical(table.Name, out var canonical))
            {
                if (!canonical.Equals(table.Name, StringComparison.Ordinal))
                {
                    rewritten = SqlText.ReplaceIdentifier(rewritten, table.Name, canonical);
                    notes.Add($"Rewrote table '{table.Name}' to schema table '{canonical}'.");
                }

                if (!string.IsNullOrWhiteSpace(table.Alias))
                    tableAliases.Add(table.Alias);
                continue;
            }

            if (tableSet.TryFuzzy(table.Name, out var fuzzy))
            {
                rewritten = SqlText.ReplaceIdentifier(rewritten, table.Name, fuzzy);
                notes.Add($"Repaired table '{table.Name}' to schema table '{fuzzy}'.");
                if (!string.IsNullOrWhiteSpace(table.Alias))
                    tableAliases.Add(table.Alias);
                continue;
            }

            errors.Add($"SQL FROM/JOIN uses unknown table '{table.Name}'. Allowed table: {tableName}.");
        }

        var aliases = ExtractSelectAliases(rewritten);
        var allowed = columns.With(aliases).With(tableAliases).With([tableName]);

        foreach (var ident in ExtractIdentifiers(rewritten))
        {
            if (ident.Length <= 1 || IsReservedToken(ident) || allowed.Contains(ident))
            {
                if (ident.Length > 1
                    && columns.TryCanonical(ident, out var canonical)
                    && !canonical.Equals(ident, StringComparison.Ordinal)
                    && !(aliases.Contains(ident) && !columns.Contains(ident))
                    && !tableAliases.Contains(ident))
                {
                    rewritten = SqlText.ReplaceIdentifier(rewritten, ident, canonical);
                    notes.Add($"Rewrote SQL identifier '{ident}' to '{canonical}'.");
                }

                continue;
            }

            if (columns.TryFuzzy(ident, out var fuzzyCol))
            {
                rewritten = SqlText.ReplaceIdentifier(rewritten, ident, fuzzyCol);
                notes.Add($"Repaired SQL identifier '{ident}' to closest column '{fuzzyCol}'.");
                continue;
            }

            errors.Add($"SQL references unknown identifier '{ident}'. Allowed columns: {columns.FormatAllowed()}.");
        }

        return new SqlGrounding(rewritten, notes, errors);
    }

    internal static IReadOnlyList<FromTable> ExtractFromJoinTables(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];
        var tables = new List<FromTable>();
        foreach (Match match in FromJoinRegex().Matches(sql))
        {
            var name = FirstGroup(match, "ident", "quoted", "backtick", "bracket");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var alias = FirstGroup(match, "aliasIdent", "aliasQuoted", "aliasBacktick", "aliasBracket");
            if (string.IsNullOrWhiteSpace(alias) || IsReservedToken(alias))
                tables.Add(new FromTable(Unquote(name), null));
            else
                tables.Add(new FromTable(Unquote(name), Unquote(alias)));
        }

        return tables;
    }

    internal static HashSet<string> ExtractSelectAliases(string? sql)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sql)) return aliases;

        foreach (Match match in AliasRegex().Matches(sql))
        {
            var alias = FirstGroup(match, "ident", "quoted", "backtick", "bracket");
            if (!string.IsNullOrWhiteSpace(alias))
                aliases.Add(Unquote(alias));
        }

        return aliases;
    }

    internal static IReadOnlyList<string> ExtractIdentifiers(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];
        var stripped = SqlText.StripCommentsAndStringLiterals(sql);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in QuotedIdentRegex().Matches(stripped))
        {
            var ident = Unquote(match.Value);
            if (!string.IsNullOrWhiteSpace(ident))
                found.Add(ident);
        }

        foreach (Match match in UnquotedIdentRegex().Matches(stripped))
        {
            if (!string.IsNullOrWhiteSpace(match.Value))
                found.Add(match.Value);
        }

        return [.. found];
    }

    private static string FirstGroup(Match match, params string[] names)
    {
        foreach (var name in names)
        {
            var group = match.Groups[name];
            if (group.Success && !string.IsNullOrWhiteSpace(group.Value))
                return group.Value;
        }

        return string.Empty;
    }

    internal static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if (value[0] == '"' && value[^1] == '"')
                return value[1..^1].Replace("\"\"", "\"");
            if (value[0] == '`' && value[^1] == '`')
                return value[1..^1].Replace("``", "`");
            if (value[0] == '[' && value[^1] == ']')
                return value[1..^1].Replace("]]", "]");
        }

        return value;
    }

    internal static bool IsReservedToken(string ident) => SqlTokens.Contains(ident);

    [GeneratedRegex(
        @"\b(?:FROM|JOIN)\s+(?!\()(?:(?<schema>[A-Za-z_][\w]*)\.)?(?:""(?<quoted>(?:[^""]|"""")+)""|`(?<backtick>[^`]+)`|\[(?<bracket>[^\]]+)\]|(?<ident>[A-Za-z_][\w]*))(?:\s+(?:AS\s+)?(?:""(?<aliasQuoted>(?:[^""]|"""")+)""|`(?<aliasBacktick>[^`]+)`|\[(?<aliasBracket>[^\]]+)\]|(?<aliasIdent>[A-Za-z_][\w]*)))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FromJoinRegex();

    [GeneratedRegex(
        @"\bAS\s+(?:""(?<quoted>(?:[^""]|"""")+)""|`(?<backtick>[^`]+)`|\[(?<bracket>[^\]]+)\]|(?<ident>[A-Za-z_][\w]*))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AliasRegex();

    [GeneratedRegex(@"""(?:[^""]|"""")+""|`[^`]+`|\[[^\]]+\]")]
    private static partial Regex QuotedIdentRegex();

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\b")]
    private static partial Regex UnquotedIdentRegex();

    private readonly record struct SqlGrounding(string Sql, List<string> Notes, List<string> Errors);

    internal readonly record struct FromTable(string Name, string? Alias);

    /// <summary>Case-insensitive identifier lookup with conservative fuzzy repair.</summary>
    internal sealed class IdentifierSet
    {
        private readonly Dictionary<string, string> _canonical = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _compact = new(StringComparer.OrdinalIgnoreCase);

        public IdentifierSet(IEnumerable<string> names)
        {
            foreach (var name in names)
                Add(name);
        }

        public int Count => _canonical.Count;

        public IdentifierSet With(IEnumerable<string> extra)
        {
            var copy = new IdentifierSet(_canonical.Values);
            foreach (var name in extra)
                copy.Add(name);
            return copy;
        }

        public void Add(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var trimmed = Unquote(name.Trim());
            if (trimmed.Length == 0) return;
            if (!_canonical.ContainsKey(trimmed))
                _canonical[trimmed] = trimmed;
            var compact = Compact(trimmed);
            if (!_compact.ContainsKey(compact))
                _compact[compact] = trimmed;
        }

        public bool Contains(string name) => _canonical.ContainsKey(Unquote(name.Trim()));

        public bool TryCanonical(string name, out string canonical)
            => _canonical.TryGetValue(Unquote(name.Trim()), out canonical!);

        public bool TryFuzzy(string name, out string canonical)
        {
            canonical = string.Empty;
            var trimmed = Unquote(name.Trim());
            if (trimmed.Length == 0) return false;

            if (_compact.TryGetValue(Compact(trimmed), out canonical!))
                return true;

            string? best = null;
            var bestDistance = int.MaxValue;
            var ties = 0;
            foreach (var candidate in _canonical.Values)
            {
                var distance = Levenshtein(trimmed.ToLowerInvariant(), candidate.ToLowerInvariant());
                if (!IsCloseEnough(trimmed.Length, candidate.Length, distance))
                    continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                    ties = 1;
                }
                else if (distance == bestDistance)
                {
                    ties++;
                }
            }

            if (best is null || ties != 1) return false;
            canonical = best;
            return true;
        }

        public string FormatAllowed()
            => string.Join(", ", _canonical.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Take(40));

        private static string Compact(string value)
            => new([.. value.Where(c => c is not '_' and not '-' and not ' ')]);

        private static bool IsCloseEnough(int fromLength, int toLength, int distance)
        {
            if (distance <= 0) return true;
            var maxLen = Math.Max(fromLength, toLength);
            if (maxLen < 4) return false;
            if (distance == 1) return true;
            // Transpositions like categroy/category are distance 2; only allow on longer names.
            return distance == 2 && maxLen >= 7;
        }

        private static int Levenshtein(string a, string b)
        {
            var n = a.Length;
            var m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (var j = 0; j <= m; j++) prev[j] = j;

            for (var i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }

                (prev, curr) = (curr, prev);
            }

            return prev[m];
        }
    }

    private static readonly HashSet<string> SqlTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "group", "by", "order", "limit", "offset", "having",
        "join", "left", "right", "inner", "outer", "full", "cross", "apply", "on", "using",
        "as", "and", "or", "not", "in", "is", "null", "like", "ilike", "between", "exists",
        "any", "some", "all", "union", "except", "intersect", "minus", "distinct", "asc", "desc",
        "case", "when", "then", "else", "end", "true", "false", "unknown", "with", "recursive",
        "over", "partition", "window", "filter", "fetch", "first", "next", "rows", "only", "row",
        "top", "into", "values", "natural", "lateral", "unnest", "ordinality", "materialized",
        "count", "sum", "avg", "min", "max", "cast", "convert", "coalesce", "nullif", "try_cast",
        "greatest", "least", "concat", "concat_ws", "substring", "substr", "trim", "ltrim", "rtrim",
        "lower", "upper", "initcap", "length", "char_length", "character_length", "replace",
        "round", "trunc", "truncate", "floor", "ceil", "ceiling", "abs", "mod", "power", "sqrt",
        "random", "generate_series", "date_trunc", "date_part", "extract", "age", "now",
        "current_date", "current_timestamp", "current_time", "localtime", "localtimestamp",
        "timezone", "at", "interval", "year", "month", "day", "hour", "minute", "second", "week",
        "quarter", "dow", "doy", "epoch", "microsecond", "millisecond", "decade", "century",
        "millennium", "to_char", "to_date", "to_timestamp", "to_number", "json_agg", "jsonb_agg",
        "json_build_object", "jsonb_build_object", "array_agg", "string_agg", "bool_and", "bool_or",
        "row_number", "rank", "dense_rank", "ntile", "lag", "lead", "first_value", "last_value",
        "nth_value", "cume_dist", "percent_rank", "percentile_cont", "percentile_disc", "listagg",
        "isnull", "ifnull", "nvl", "nvl2", "datediff", "dateadd", "datepart", "getdate",
        "sysdatetime", "format", "iif", "choose", "stuff", "charindex", "patindex", "len",
        "left", "right", "lpad", "rpad", "position", "strpos", "split_part", "regexp_replace",
        "regexp_matches", "md5", "uuid", "gen_random_uuid", "nulls", "last", "collate", "similar",
        "escape", "integer", "int", "bigint", "smallint", "decimal", "numeric", "real", "double",
        "precision", "float", "boolean", "bool", "text", "varchar", "char", "nchar", "nvarchar",
        "date", "time", "timestamp", "timestamptz", "timetz", "json", "jsonb", "bytea", "blob",
        "money", "serial", "bigserial", "if", "both", "leading", "trailing", "for", "share",
        "update", "of", "nowait", "skip", "locked", "unique", "value", "key", "index", "percent",
        "ties", "unbounded", "preceding", "following", "current", "range", "groups", "exclude",
        "search", "cycle", "schema", "public", "dbo", "sys", "pg_catalog", "information_schema",
        "dual", "zone", "array", "string", "int2", "int4", "int8", "float4", "float8", "bpchar",
        "name", "oid", "regclass", "record", "anyelement", "void", "sql", "grouping", "rollup",
        "cube", "sets", "within", "others", "no", "yes", "to", "try_convert", "parse", "try_parse",
        "overlay", "placing", "sysdate", "systimestamp", "newid", "returning", "session_user",
        "current_user", "current_role", "current_catalog", "current_schema", "unsigned", "signed",
        "binary", "varbinary", "bit", "group_concat", "std", "stddev", "variance", "var_pop",
        "var_samp", "bit_and", "bit_or", "bit_xor", "any_value", "json_arrayagg", "json_objectagg",
        "json_extract", "json_unquote", "json_object", "json_array", "json_contains", "json_keys",
        "date_add", "date_sub", "date_format", "from_unixtime", "unix_timestamp", "str_to_date",
        "time_format", "timediff", "timestampadd", "timestampdiff", "utc_date", "utc_time",
        "utc_timestamp", "convert_tz", "adddate", "addtime", "subdate", "subtime", "makedate",
        "maketime", "last_day", "to_days", "from_days", "sec_to_time", "time_to_sec", "dayname",
        "dayofmonth", "dayofweek", "dayofyear", "monthname", "weekday", "weekofyear", "yearweek",
        "curdate", "curtime", "datetime", "clob", "identity", "database", "table", "column",
        "view", "type", "every", "xmlagg", "make_date", "make_time", "make_timestamp",
        "justify_hours", "justify_days", "justify_interval", "width_bucket", "div", "instr",
        "ascii", "chr", "quote", "repeat", "reverse", "space", "strcmp", "ucase", "lcase",
        "acos", "asin", "atan", "atan2", "atn2", "cos", "cot", "crc32", "degrees", "exp", "ln",
        "log", "log10", "log2", "pi", "pow", "radians", "rand", "sign", "sin", "tan", "square",
        "conv", "insert", "locate", "mid", "translate", "unicode", "nchar", "datalength",
        "replicate", "eomonth", "switchoffset", "todatetimeoffset", "datefromparts", "datename",
        "json_each", "jsonb_each", "json_array_elements", "jsonb_array_elements",
        "regexp_split_to_table", "string_to_array", "decode", "if", "end"
    };
}

/// <summary>
/// Walks SQL text so identifier rewrites do not touch string literals.
/// </summary>
internal static class SqlText
{
    public static string StripCommentsAndStringLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            if (i < sql.Length - 1 && sql[i] == '-' && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] is not '\n')
                    i++;
                sb.Append(' ');
                continue;
            }

            if (i < sql.Length - 1 && sql[i] == '/' && sql[i + 1] == '*')
            {
                i += 2;
                while (i < sql.Length - 1 && !(sql[i] == '*' && sql[i + 1] == '/'))
                    i++;
                i = Math.Min(sql.Length, i + 2);
                sb.Append(' ');
                continue;
            }

            if (sql[i] == '\'')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    if (sql[i] == '\'')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                sb.Append("''");
                continue;
            }

            sb.Append(sql[i]);
            i++;
        }

        return sb.ToString();
    }

    public static string ReplaceIdentifier(string sql, string from, string to)
    {
        if (string.IsNullOrEmpty(from) || from.Equals(to, StringComparison.Ordinal))
            return sql;

        var sb = new StringBuilder(sql.Length + 8);
        var i = 0;
        while (i < sql.Length)
        {
            if (sql[i] == '\'')
            {
                sb.Append(sql[i]);
                i++;
                while (i < sql.Length)
                {
                    sb.Append(sql[i]);
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        sb.Append(sql[i + 1]);
                        i += 2;
                        continue;
                    }

                    if (sql[i] == '\'')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (TryMatchQuoted(sql, i, '"', from, out var dqLen))
            {
                sb.Append('"').Append(to.Replace("\"", "\"\"")).Append('"');
                i += dqLen;
                continue;
            }

            if (TryMatchQuoted(sql, i, '`', from, out var btLen))
            {
                sb.Append('`').Append(to.Replace("`", "``")).Append('`');
                i += btLen;
                continue;
            }

            if (sql[i] == '[' && TryMatchBracket(sql, i, from, out var brLen))
            {
                sb.Append('[').Append(to.Replace("]", "]]")).Append(']');
                i += brLen;
                continue;
            }

            if (IsIdentStart(sql[i]) && TryMatchUnquoted(sql, i, from, out var idLen))
            {
                sb.Append(to);
                i += idLen;
                continue;
            }

            sb.Append(sql[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool TryMatchQuoted(string sql, int i, char quote, string from, out int length)
    {
        length = 0;
        if (sql[i] != quote) return false;
        var j = i + 1;
        var ident = new StringBuilder();
        while (j < sql.Length)
        {
            if (sql[j] == quote)
            {
                if (j + 1 < sql.Length && sql[j + 1] == quote)
                {
                    ident.Append(quote);
                    j += 2;
                    continue;
                }

                j++;
                break;
            }

            ident.Append(sql[j]);
            j++;
        }

        if (!ident.ToString().Equals(from, StringComparison.OrdinalIgnoreCase))
            return false;
        length = j - i;
        return true;
    }

    private static bool TryMatchBracket(string sql, int i, string from, out int length)
    {
        length = 0;
        if (sql[i] != '[') return false;
        var j = i + 1;
        var ident = new StringBuilder();
        while (j < sql.Length)
        {
            if (sql[j] == ']' && j + 1 < sql.Length && sql[j + 1] == ']')
            {
                ident.Append(']');
                j += 2;
                continue;
            }

            if (sql[j] == ']')
            {
                j++;
                break;
            }

            ident.Append(sql[j]);
            j++;
        }

        if (!ident.ToString().Equals(from, StringComparison.OrdinalIgnoreCase))
            return false;
        length = j - i;
        return true;
    }

    private static bool TryMatchUnquoted(string sql, int i, string from, out int length)
    {
        length = 0;
        var j = i;
        while (j < sql.Length && IsIdentPart(sql[j]))
            j++;
        var ident = sql[i..j];
        if (!ident.Equals(from, StringComparison.OrdinalIgnoreCase))
            return false;
        if (i > 0 && IsIdentPart(sql[i - 1]))
            return false;
        length = j - i;
        return true;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
