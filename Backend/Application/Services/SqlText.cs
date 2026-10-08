using System.Text;

namespace Application.Services;

/// <summary>
/// Literal-safe SQL identifier rewrites used when grounding unique near-miss names.
/// Never replaces tokens inside single-quoted string literals.
/// </summary>
internal static class SqlText
{
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
                sb.Append('"').Append(to.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                i += dqLen;
                continue;
            }

            if (TryMatchQuoted(sql, i, '`', from, out var btLen))
            {
                sb.Append('`').Append(to.Replace("`", "``", StringComparison.Ordinal)).Append('`');
                i += btLen;
                continue;
            }

            if (sql[i] == '[' && TryMatchBracket(sql, i, from, out var brLen))
            {
                sb.Append('[').Append(to.Replace("]", "]]", StringComparison.Ordinal)).Append(']');
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
