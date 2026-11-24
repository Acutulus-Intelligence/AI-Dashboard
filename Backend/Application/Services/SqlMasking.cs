using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Masks SQL string literals, quoted identifiers and comments before keyword
/// checks. Masked characters become spaces so token boundaries are preserved.
/// This keeps validation from being fooled by <c>INTO</c> inside a literal and
/// from rejecting harmless words like <c>DROP</c> in a string.
/// </summary>
public static class SqlMasking
{
    public static string MaskLiteralsAndComments(string sql) =>
        MaskLiteralsAndComments(sql, provider: null);

    public static string MaskLiteralsAndComments(
        string sql,
        DbProvider? provider,
        bool maskAllLineComments = false)
    {
        if (string.IsNullOrEmpty(sql))
            return string.Empty;

        var buffer = new char[sql.Length];
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            // Line comment. MySQL only treats "--" as a comment when followed by
            // whitespace; PostgreSQL, SQL Server and SQLite treat every "--" as a
            // comment. The no-provider overload keeps the conservative MySQL rule
            // so it never hides a MySQL expression from validation.
            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                var isComment =
                    maskAllLineComments
                    || provider is DbProvider.PostgreSql or DbProvider.SqlServer or DbProvider.Sqlite
                    || ((provider is null or DbProvider.MySql)
                        && (i + 2 >= sql.Length || char.IsWhiteSpace(sql[i + 2])));

                if (isComment)
                {
                    while (i < sql.Length && sql[i] != '\n')
                        buffer[i++] = ' ';
                    continue;
                }
            }

            // MySQL supports "#" line comments. In other dialects "#" is an
            // operator (PostgreSQL) or part of a temp-table name (SQL Server).
            // Log redaction masks it regardless of dialect (over-redaction is safe).
            if (c == '#' && (maskAllLineComments || provider is DbProvider.MySql))
            {
                while (i < sql.Length && sql[i] != '\n')
                    buffer[i++] = ' ';
                continue;
            }

            // Block comment. MySQL executable comments (/*! ... */) are rejected
            // separately by the validator, so masking them here is safe.
            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                buffer[i++] = ' ';
                buffer[i++] = ' ';
                while (i < sql.Length)
                {
                    if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        buffer[i++] = ' ';
                        buffer[i++] = ' ';
                        break;
                    }

                    buffer[i++] = ' ';
                }

                continue;
            }

            // Single-quoted string with '' and backslash escapes.
            if (c == '\'')
            {
                buffer[i++] = ' ';
                while (i < sql.Length)
                {
                    if (sql[i] == '\\' && i + 1 < sql.Length)
                    {
                        buffer[i++] = ' ';
                        buffer[i++] = ' ';
                        continue;
                    }

                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'')
                        {
                            buffer[i++] = ' ';
                            buffer[i++] = ' ';
                            continue;
                        }

                        buffer[i++] = ' ';
                        break;
                    }

                    buffer[i++] = ' ';
                }

                continue;
            }

            if (c == '"')
            {
                MaskDelimited(sql, buffer, ref i, '"');
                continue;
            }

            if (c == '`')
            {
                MaskDelimited(sql, buffer, ref i, '`');
                continue;
            }

            if (c == '[')
            {
                MaskDelimited(sql, buffer, ref i, ']');
                continue;
            }

            // PostgreSQL dollar-quoted string ($$...$$ or $tag$...$tag$).
            if (c == '$' && TryReadDollarTag(sql, i, out var tagEnd, out var tag))
            {
                var close = sql.IndexOf(tag, tagEnd, StringComparison.Ordinal);
                var end = close < 0 ? sql.Length : close + tag.Length;
                while (i < end)
                    buffer[i++] = ' ';
                continue;
            }

            buffer[i] = c;
            i++;
        }

        return new string(buffer);
    }

    private static void MaskDelimited(string sql, char[] buffer, ref int i, char close)
    {
        buffer[i++] = ' ';
        while (i < sql.Length)
        {
            if (sql[i] == close)
            {
                if (i + 1 < sql.Length && sql[i + 1] == close)
                {
                    buffer[i++] = ' ';
                    buffer[i++] = ' ';
                    continue;
                }

                buffer[i++] = ' ';
                break;
            }

            buffer[i++] = ' ';
        }
    }

    private static bool TryReadDollarTag(string sql, int start, out int tagEnd, out string tag)
    {
        tagEnd = start;
        tag = string.Empty;

        var j = start + 1;
        while (j < sql.Length && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_'))
            j++;

        if (j >= sql.Length || sql[j] != '$')
            return false;

        tag = sql[start..(j + 1)];
        tagEnd = j + 1;
        return true;
    }
}
