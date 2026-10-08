using System.Text;

namespace Application.Services;

/// <summary>
/// Checks that a SELECT references only the connected table and its columns.
/// Invented tables/columns are rejected before the query is executed.
/// </summary>
public static class SqlSchemaGrounder
{
    public static bool TryValidate(
        string sql,
        string allowedTable,
        IReadOnlyList<string> allowedColumns,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(allowedTable))
        {
            errorMessage = "No allowed table was provided for schema grounding.";
            return false;
        }

        var tokens = Tokenize(sql);
        if (tokens.Count == 0)
        {
            errorMessage = "SQL query contains no identifiers to validate.";
            return false;
        }

        var allowedTableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Unquote(allowedTable) };
        var allowedColumnSet = new HashSet<string>(
            allowedColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Unquote),
            StringComparer.OrdinalIgnoreCase);

        var cteNames = ExtractCteNames(tokens);
        var tableAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedTables = ExtractReferencedTables(tokens, cteNames, tableAliases);

        var unknownTables = referencedTables
            .Where(t => !allowedTableSet.Contains(t) && !cteNames.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknownTables.Count > 0)
        {
            errorMessage =
                $"Unknown table(s) in SQL: {string.Join(", ", unknownTables)}. " +
                $"Allowed tables: {allowedTable}.";
            return false;
        }

        if (referencedTables.Count == 0 && cteNames.Count == 0)
        {
            errorMessage =
                $"SQL does not reference the allowed table '{allowedTable}'.";
            return false;
        }

        var selectAliases = ExtractSelectAliases(tokens);
        var known = new HashSet<string>(allowedColumnSet, StringComparer.OrdinalIgnoreCase);
        foreach (var alias in selectAliases) known.Add(alias);
        foreach (var cte in cteNames) known.Add(cte);
        foreach (var table in allowedTableSet) known.Add(table);
        foreach (var alias in tableAliases) known.Add(alias);

        var unknownColumns = ExtractColumnReferences(tokens)
            .Where(c => !known.Contains(c) && !IsNonColumnIdentifier(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknownColumns.Count > 0)
        {
            errorMessage =
                $"Unknown column(s) in SQL: {string.Join(", ", unknownColumns)}. " +
                $"Allowed columns: {string.Join(", ", allowedColumnSet)}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Rewrites unique near-miss table/column names onto the connected schema, then
    /// validates. Invented identifiers with no unique match still fail.
    /// </summary>
    public static bool TryGround(
        string sql,
        string allowedTable,
        IReadOnlyList<string> allowedColumns,
        out string groundedSql,
        out string? errorMessage)
    {
        groundedSql = sql ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(allowedTable))
            return TryValidate(sql ?? string.Empty, allowedTable, allowedColumns, out errorMessage);

        var tables = new SchemaIdentifierSet([allowedTable]);
        var columns = new SchemaIdentifierSet(allowedColumns);
        var rewritten = sql;

        rewritten = RewriteUnknownTables(rewritten, tables);
        rewritten = RewriteUnknownColumns(rewritten, allowedTable, columns);
        groundedSql = rewritten;
        return TryValidate(groundedSql, allowedTable, allowedColumns, out errorMessage);
    }

    private static string RewriteUnknownTables(string sql, SchemaIdentifierSet tables)
    {
        var tokens = Tokenize(sql);
        var cteNames = ExtractCteNames(tokens);
        var tableAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenced = ExtractReferencedTables(tokens, cteNames, tableAliases);

        foreach (var table in referenced)
        {
            if (tables.Contains(table) || cteNames.Contains(table))
                continue;
            if (tables.TryFuzzy(table, out var canonical))
                sql = SqlText.ReplaceIdentifier(sql, table, canonical);
        }

        return sql;
    }

    private static string RewriteUnknownColumns(string sql, string allowedTable, SchemaIdentifierSet columns)
    {
        var tokens = Tokenize(sql);
        var cteNames = ExtractCteNames(tokens);
        var tableAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractReferencedTables(tokens, cteNames, tableAliases);
        var selectAliases = ExtractSelectAliases(tokens);

        var known = columns.With(
            selectAliases
                .Concat(cteNames)
                .Concat(tableAliases)
                .Append(Unquote(allowedTable)));

        var unknown = ExtractColumnReferences(tokens)
            .Where(c => !known.Contains(c) && !IsNonColumnIdentifier(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var column in unknown)
        {
            if (columns.TryFuzzy(column, out var canonical))
                sql = SqlText.ReplaceIdentifier(sql, column, canonical);
        }

        return sql;
    }

    internal static List<string> ExtractSelectAliases(IReadOnlyList<SqlToken> tokens)
    {
        var aliases = new List<string>();
        var selectIdx = IndexOfKeyword(tokens, "SELECT", 0);
        if (selectIdx < 0) return aliases;

        var fromIdx = IndexOfKeywordAtDepth(tokens, "FROM", selectIdx + 1, depth: 0);
        var end = fromIdx < 0 ? tokens.Count : fromIdx;

        for (var i = selectIdx + 1; i < end - 1; i++)
        {
            if (tokens[i].Kind != TokenKind.Keyword || !tokens[i].ValueEquals("AS"))
                continue;

            var next = tokens[i + 1];
            if (next.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
                aliases.Add(next.Normalized);
        }

        return aliases;
    }

    internal static HashSet<string> ExtractCteNames(IReadOnlyList<SqlToken> tokens)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tokens.Count == 0 || tokens[0].Kind != TokenKind.Keyword || !tokens[0].ValueEquals("WITH"))
            return names;

        var i = 1;
        if (i < tokens.Count && tokens[i].Kind == TokenKind.Keyword && tokens[i].ValueEquals("RECURSIVE"))
            i++;

        while (i < tokens.Count)
        {
            if (tokens[i].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
            {
                names.Add(tokens[i].Normalized);
                i++;
            }

            while (i < tokens.Count && !(tokens[i].Kind == TokenKind.Punctuation && tokens[i].Value == "("))
            {
                if (tokens[i].Kind == TokenKind.Keyword && tokens[i].ValueEquals("SELECT") && ParenDepth(tokens, i) == 0)
                    return names;
                i++;
            }

            if (i >= tokens.Count) break;
            i = SkipBalanced(tokens, i) + 1;

            if (i < tokens.Count && tokens[i].Kind == TokenKind.Punctuation && tokens[i].Value == ",")
            {
                i++;
                continue;
            }

            break;
        }

        return names;
    }

    internal static HashSet<string> ExtractReferencedTables(
        IReadOnlyList<SqlToken> tokens,
        IReadOnlySet<string> cteNames,
        HashSet<string> tableAliases)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.Keyword)
                continue;
            if (!tokens[i].ValueEquals("FROM") && !tokens[i].ValueEquals("JOIN"))
                continue;

            var j = i + 1;
            j = SkipKeywords(tokens, j, "LATERAL", "ONLY");
            if (j >= tokens.Count) break;

            if (tokens[j].Kind == TokenKind.Punctuation && tokens[j].Value == "(")
                continue;

            if (tokens[j].Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier))
                continue;

            var table = ReadQualifiedName(tokens, ref j);
            if (table.Count == 0) continue;

            var tableName = table[^1];
            if (!cteNames.Contains(tableName))
                tables.Add(tableName);

            j++;
            if (j < tokens.Count && tokens[j].Kind == TokenKind.Keyword && tokens[j].ValueEquals("AS"))
                j++;

            if (j < tokens.Count
                && tokens[j].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
                && !IsJoinBoundary(tokens[j]))
            {
                tableAliases.Add(tokens[j].Normalized);
            }
        }

        return tables;
    }

    internal static List<string> ExtractColumnReferences(IReadOnlyList<SqlToken> tokens)
    {
        var columns = new List<string>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier))
                continue;

            if (IsKeywordOrType(token.Normalized))
                continue;

            // function call: IDENT(
            if (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Punctuation && tokens[i + 1].Value == "(")
                continue;

            // type cast: ::IDENT
            if (i > 0 && tokens[i - 1].Kind == TokenKind.Punctuation && tokens[i - 1].Value == "::")
                continue;

            // CAST(x AS type) — identifier after AS at depth > 0 with CAST in play
            if (i > 0
                && tokens[i - 1].Kind == TokenKind.Keyword
                && tokens[i - 1].ValueEquals("AS")
                && IsKeywordOrType(token.Normalized))
                continue;

            // qualified name: take the last segment as the column (schema.table.column or alias.column)
            if (i + 2 < tokens.Count
                && tokens[i + 1].Kind == TokenKind.Punctuation
                && tokens[i + 1].Value == "."
                && tokens[i + 2].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
            {
                continue; // the last segment is visited on its own iteration
            }

            if (i >= 2
                && tokens[i - 1].Kind == TokenKind.Punctuation
                && tokens[i - 1].Value == ".")
            {
                columns.Add(token.Normalized);
                continue;
            }

            columns.Add(token.Normalized);
        }

        return columns;
    }

    internal static List<SqlToken> Tokenize(string sql)
    {
        var tokens = new List<SqlToken>();
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i = sql.IndexOf('\n', i);
                if (i < 0) break;
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? sql.Length : end + 2;
                continue;
            }

            if (c is '\'' or '"' or '`' or '[')
            {
                tokens.Add(ReadQuoted(sql, ref i));
                continue;
            }

            if (c == ':' && i + 1 < sql.Length && sql[i + 1] == ':')
            {
                tokens.Add(new SqlToken(TokenKind.Punctuation, "::"));
                i += 2;
                continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.'))
                    i++;
                tokens.Add(new SqlToken(TokenKind.Number, sql[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                    i++;
                var raw = sql[start..i];
                var kind = Keywords.Contains(raw) ? TokenKind.Keyword : TokenKind.Identifier;
                tokens.Add(new SqlToken(kind, raw));
                continue;
            }

            tokens.Add(new SqlToken(TokenKind.Punctuation, c.ToString()));
            i++;
        }

        return tokens;
    }

    private static SqlToken ReadQuoted(string sql, ref int i)
    {
        var opener = sql[i];
        var closer = opener == '[' ? ']' : opener;
        var escape = opener != '[';
        i++;
        var sb = new StringBuilder();
        while (i < sql.Length)
        {
            var c = sql[i];
            if (escape && c == opener && i + 1 < sql.Length && sql[i + 1] == opener)
            {
                sb.Append(c);
                i += 2;
                continue;
            }

            if (c == closer)
            {
                i++;
                break;
            }

            sb.Append(c);
            i++;
        }

        var value = sb.ToString();
        return opener == '\''
            ? new SqlToken(TokenKind.String, value)
            : new SqlToken(TokenKind.QuotedIdentifier, value);
    }

    private static List<string> ReadQualifiedName(IReadOnlyList<SqlToken> tokens, ref int j)
    {
        var parts = new List<string>();
        while (j < tokens.Count && tokens[j].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
        {
            parts.Add(tokens[j].Normalized);
            if (j + 1 < tokens.Count
                && tokens[j + 1].Kind == TokenKind.Punctuation
                && tokens[j + 1].Value == ".")
            {
                j += 2;
                continue;
            }

            break;
        }

        return parts;
    }

    private static int SkipKeywords(IReadOnlyList<SqlToken> tokens, int j, params string[] keywords)
    {
        while (j < tokens.Count
               && tokens[j].Kind == TokenKind.Keyword
               && keywords.Any(k => tokens[j].ValueEquals(k)))
        {
            j++;
        }

        return j;
    }

    private static int SkipBalanced(IReadOnlyList<SqlToken> tokens, int openIdx)
    {
        var depth = 0;
        for (var i = openIdx; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.Punctuation) continue;
            if (tokens[i].Value == "(") depth++;
            else if (tokens[i].Value == ")")
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return tokens.Count - 1;
    }

    private static int ParenDepth(IReadOnlyList<SqlToken> tokens, int idx)
    {
        var depth = 0;
        for (var i = 0; i < idx && i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.Punctuation) continue;
            if (tokens[i].Value == "(") depth++;
            else if (tokens[i].Value == ")") depth--;
        }

        return depth;
    }

    private static int IndexOfKeyword(IReadOnlyList<SqlToken> tokens, string keyword, int start)
    {
        for (var i = start; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.Keyword && tokens[i].ValueEquals(keyword))
                return i;
        }

        return -1;
    }

    private static int IndexOfKeywordAtDepth(IReadOnlyList<SqlToken> tokens, string keyword, int start, int depth)
    {
        var current = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.Punctuation)
            {
                if (tokens[i].Value == "(") current++;
                else if (tokens[i].Value == ")") current--;
            }

            if (i >= start
                && current == depth
                && tokens[i].Kind == TokenKind.Keyword
                && tokens[i].ValueEquals(keyword))
                return i;
        }

        return -1;
    }

    private static bool IsJoinBoundary(SqlToken token)
        => token.Kind == TokenKind.Keyword && JoinBoundaryKeywords.Contains(token.Normalized);

    private static bool IsKeywordOrType(string value)
        => Keywords.Contains(value) || TypeNames.Contains(value) || DateParts.Contains(value);

    private static bool IsNonColumnIdentifier(string value)
        => IsKeywordOrType(value);

    internal static string Unquote(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length >= 2)
        {
            if (trimmed[0] == '"' && trimmed[^1] == '"')
                return trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
            if (trimmed[0] == '`' && trimmed[^1] == '`')
                return trimmed[1..^1].Replace("``", "`", StringComparison.Ordinal);
            if (trimmed[0] == '[' && trimmed[^1] == ']')
                return trimmed[1..^1].Replace("]]", "]", StringComparison.Ordinal);
        }

        return trimmed;
    }

    internal readonly record struct SqlToken(TokenKind Kind, string Value)
    {
        public string Normalized => Value;
        public bool ValueEquals(string other) => Value.Equals(other, StringComparison.OrdinalIgnoreCase);
    }

    internal enum TokenKind
    {
        Identifier,
        QuotedIdentifier,
        Keyword,
        Number,
        String,
        Punctuation,
    }

    private static readonly HashSet<string> JoinBoundaryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ON", "USING", "WHERE", "GROUP", "ORDER", "HAVING", "LIMIT", "OFFSET",
        "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS", "FULL", "NATURAL",
        "UNION", "EXCEPT", "INTERSECT", "FETCH", "WINDOW", "QUALIFY",
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "INT", "INTEGER", "BIGINT", "SMALLINT", "TINYINT", "SERIAL", "BIGSERIAL",
        "DECIMAL", "NUMERIC", "FLOAT", "REAL", "DOUBLE", "PRECISION", "MONEY",
        "BOOLEAN", "BOOL", "BIT", "BYTE", "BYTEA",
        "CHAR", "CHARACTER", "VARCHAR", "NVARCHAR", "NCHAR", "TEXT", "CLOB", "BLOB",
        "DATE", "TIME", "TIMESTAMP", "TIMESTAMPTZ", "DATETIME", "INTERVAL",
        "UUID", "JSON", "JSONB", "XML", "ARRAY", "RECORD",
        "VARYING", "UNSIGNED", "SIGNED", "ZONE",
    };

    private static readonly HashSet<string> DateParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "YEAR", "MONTH", "DAY", "HOUR", "MINUTE", "SECOND", "WEEK", "QUARTER",
        "DOW", "DOY", "EPOCH", "MICROSECONDS", "MILLISECONDS", "CENTURY",
        "DECADE", "MILLENNIUM", "TIMEZONE", "ISODOW", "ISOYEAR",
    };

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS",
        "FULL", "NATURAL", "LATERAL", "ONLY", "ON", "USING", "AND", "OR", "NOT", "IN",
        "IS", "NULL", "AS", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET",
        "DISTINCT", "ALL", "CASE", "WHEN", "THEN", "ELSE", "END", "BETWEEN", "LIKE",
        "ILIKE", "SIMILAR", "ESCAPE", "EXISTS", "UNION", "EXCEPT", "INTERSECT",
        "ASC", "DESC", "WITH", "RECURSIVE", "CAST", "TRUE", "FALSE", "OVER",
        "PARTITION", "ROWS", "RANGE", "UNBOUNDED", "PRECEDING", "FOLLOWING",
        "CURRENT", "ROW", "FILTER", "WINDOW", "FETCH", "FIRST", "NEXT", "TIES",
        "NULLS", "LAST", "VALUES", "TABLE", "UNNEST", "COALESCE", "NULLIF",
        "GREATEST", "LEAST", "EXTRACT", "TRIM", "SUBSTRING", "POSITION",
        "BOTH", "LEADING", "TRAILING", "FOR", "SYMMETRIC", "ASYMMETRIC",
        "SOME", "ANY", "UNIQUE", "PRIMARY", "FOREIGN", "REFERENCES",
        "COLLATE", "INTERVAL", "TOP", "PERCENT", "WITHIN", "GROUPING",
        "ROLLUP", "CUBE", "SETS", "QUALIFY", "EXCLUDE", "OTHERS",
        "MATERIALIZED", "SEARCH", "CYCLE", "ORDINALITY",
        "INSERT", "UPDATE", "DELETE", "INTO", "SET", "RETURNING",
        "COUNT", "SUM", "AVG", "MIN", "MAX", "STDDEV", "VARIANCE",
        "NOW", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP",
        "LOCALTIME", "LOCALTIMESTAMP", "USER", "SESSION_USER", "CURRENT_USER",
        "IF", "IFF", "NVL", "ISNULL", "NOTNULL",
        "INNER", "APPLY", "PIVOT", "UNPIVOT",
        "DESC", "ASC",
    };
}
