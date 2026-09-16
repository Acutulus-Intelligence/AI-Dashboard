using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Conservative identifier extraction from SELECT SQL so invented tables/columns
/// can be rejected without executing the query.
/// </summary>
public static partial class AiSqlIdentifierScanner
{
    public static IReadOnlyList<string> ExtractReferencedTables(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];

        var cleaned = StripCommentsAndStringLiterals(sql);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = new List<string>();

        foreach (Match match in TableRefRegex().Matches(cleaned))
        {
            var name = Unquote(match.Groups["table"].Value);
            if (string.IsNullOrWhiteSpace(name) || IsReserved(name)) continue;
            if (seen.Add(name))
                tables.Add(name);
        }

        return tables;
    }

    public static IReadOnlyList<string> ExtractRelationAliases(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];

        var cleaned = StripCommentsAndStringLiterals(sql);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new List<string>();

        foreach (Match match in TableRefRegex().Matches(cleaned))
        {
            var name = Unquote(match.Groups["alias"].Value);
            if (string.IsNullOrWhiteSpace(name) || IsReserved(name)) continue;
            if (seen.Add(name))
                aliases.Add(name);
        }

        return aliases;
    }

    public static IReadOnlyList<string> ExtractSelectAliases(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];

        var cleaned = StripCommentsAndStringLiterals(sql);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new List<string>();

        foreach (Match match in AliasRegex().Matches(cleaned))
        {
            var name = Unquote(match.Groups["alias"].Value);
            if (string.IsNullOrWhiteSpace(name) || IsReserved(name)) continue;
            if (seen.Add(name))
                aliases.Add(name);
        }

        return aliases;
    }

    public static IReadOnlyList<string> ExtractLikelyColumnReferences(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];

        var cleaned = StripCommentsAndStringLiterals(sql);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new List<string>();

        foreach (Match match in QualifiedColumnRegex().Matches(cleaned))
        {
            var name = Unquote(match.Groups["column"].Value);
            AddColumn(name, seen, columns);
        }

        foreach (Match match in IdentifierRegex().Matches(cleaned))
        {
            var name = Unquote(match.Value);
            AddColumn(name, seen, columns);
        }

        return columns;
    }

    public static string StripCommentsAndStringLiterals(string sql)
    {
        var withoutLineComments = LineCommentRegex().Replace(sql, " ");
        var withoutBlockComments = BlockCommentRegex().Replace(withoutLineComments, " ");
        var withoutDollarQuotes = DollarQuoteRegex().Replace(withoutBlockComments, "''");
        return StringLiteralRegex().Replace(withoutDollarQuotes, "''");
    }

    public static string Unquote(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length >= 2)
        {
            if (trimmed[0] == '"' && trimmed[^1] == '"')
                return trimmed[1..^1].Replace("\"\"", "\"");
            if (trimmed[0] == '`' && trimmed[^1] == '`')
                return trimmed[1..^1].Replace("``", "`");
            if (trimmed[0] == '[' && trimmed[^1] == ']')
                return trimmed[1..^1].Replace("]]", "]");
        }

        return trimmed;
    }

    private static void AddColumn(string name, HashSet<string> seen, List<string> columns)
    {
        if (string.IsNullOrWhiteSpace(name) || IsReserved(name)) return;
        if (seen.Add(name))
            columns.Add(name);
    }

    public static bool IsReserved(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return true;
        if (identifier.All(c => char.IsDigit(c) || c == '.')) return true;
        return SqlReserved.Contains(identifier);
    }

    [GeneratedRegex(@"--.*?$", RegexOptions.Multiline)]
    private static partial Regex LineCommentRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"\$[A-Za-z0-9_]*\$.*?\$[A-Za-z0-9_]*\$", RegexOptions.Singleline)]
    private static partial Regex DollarQuoteRegex();

    [GeneratedRegex(@"N?'(?:''|[^'])*'", RegexOptions.IgnoreCase)]
    private static partial Regex StringLiteralRegex();

    private const string Ident = @"(?:""[^""]+""|`[^`]+`|\[[^\]]+\]|[A-Za-z_][\w$]*)";

    [GeneratedRegex(
        @"\b(?:FROM|JOIN)\s+(?:ONLY\s+)?(?:LATERAL\s+)?(?!\()(?:(?:" + Ident + @")\s*\.\s*)?(?<table>" + Ident + @")(?:\s+(?:AS\s+)?(?<alias>" + Ident + @"))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableRefRegex();

    [GeneratedRegex(
        @"\bAS\s+(?<alias>" + Ident + @")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AliasRegex();

    [GeneratedRegex(
        @"(?:" + Ident + @")\s*\.\s*(?<column>" + Ident + @")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedColumnRegex();

    [GeneratedRegex(Ident, RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// SQL keywords and common functions so they are not treated as column names.
    /// </summary>
    private static readonly HashSet<string> SqlReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "group", "by", "order", "having", "limit", "offset",
        "join", "inner", "left", "right", "full", "outer", "cross", "on", "as", "and", "or",
        "not", "in", "is", "null", "true", "false", "like", "ilike", "between", "exists",
        "case", "when", "then", "else", "end", "distinct", "all", "union", "except", "intersect",
        "asc", "desc", "nulls", "first", "last", "over", "partition", "rows", "range",
        "unbounded", "preceding", "following", "current", "row", "filter", "within",
        "with", "recursive", "only", "lateral", "using", "natural", "cast", "try_cast",
        "window", "fetch", "next", "percent", "ties", "top", "into", "values",
        "count", "sum", "avg", "min", "max", "coalesce", "nullif", "greatest", "least",
        "abs", "round", "trunc", "truncate", "ceil", "ceiling", "floor", "mod", "power",
        "sqrt", "exp", "ln", "log", "sign", "width_bucket",
        "date_trunc", "date_part", "extract", "age", "now", "current_date", "current_timestamp",
        "current_time", "localtime", "localtimestamp", "timezone", "at", "time", "zone",
        "interval", "timestamp", "timestamptz", "date", "year", "month", "day", "hour",
        "minute", "second", "week", "quarter", "epoch", "dow", "doy",
        "to_char", "to_date", "to_timestamp", "to_number", "format",
        "concat", "concat_ws", "length", "char_length", "character_length", "lower", "upper",
        "trim", "ltrim", "rtrim", "left", "right", "substr", "substring", "replace", "position",
        "strpos", "split_part", "regexp_replace", "regexp_split_to_array",
        "json_build_object", "jsonb_build_object", "json_agg", "jsonb_agg", "array_agg",
        "string_agg", "bool_and", "bool_or", "every",
        "row_number", "rank", "dense_rank", "ntile", "lag", "lead", "first_value", "last_value",
        "generate_series", "unnest", "json_to_recordset", "jsonb_to_recordset",
        "ifnull", "nvl", "isnull", "if", "dateadd", "datediff", "datepart", "getdate",
        "sysdate", "convert", "try_convert", "iif", "choose", "format",
        "numeric", "decimal", "integer", "int", "bigint", "smallint", "float", "real",
        "double", "precision", "varchar", "nvarchar", "char", "text", "boolean", "bool",
        "over", "collate", "asc", "desc",
        "public", "dbo", "sys", "information_schema", "pg_catalog", "mysql",
        "dual", "std", "stddev", "variance", "var_pop", "var_samp",
        "percentile_cont", "percentile_disc", "mode", "grouping", "rollup", "cube",
        "sets", "unique", "primary", "key", "index",
    };
}
