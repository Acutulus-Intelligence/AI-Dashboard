using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Enums;
using gudusoft.gsqlparser;

namespace Infrastructure.ExternalDb.Services;

public partial class SqlValidator : ISqlValidator
{
    private static readonly HashSet<string> ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "CREATE", "EXEC", "EXECUTE", "MERGE", "CALL", "REPLACE",
        "GRANT", "REVOKE", "RENAME", "LOAD", "IMPORT", "COPY"
    ];

    private static readonly string[] BlockedFunctions =
    [
        "pg_sleep", "pg_read_file", "pg_write_file", "lo_import", "lo_export",
        "dblink", "dblink_exec", "pg_terminate_backend", "pg_cancel_backend",
        "sleep", "benchmark", "load_file", "sys_eval", "sys_exec"
    ];

    public bool IsSelectOnly(string sql, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        if (ContainsBlockedFunction(sql))
        {
            errorMessage = "Query contains a blocked function.";
            return false;
        }

        if (ContainsIntoClause(sql))
        {
            errorMessage = "Query contains a forbidden INTO clause.";
            return false;
        }

        if (!TryParseAsSelectOnly(sql, out errorMessage))
            return false;

        if (ContainsForbiddenKeyword(sql))
        {
            errorMessage = "Query contains forbidden SQL statements.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryParseAsSelectOnly(string sql, out string? errorMessage)
    {
        foreach (var vendor in new[] { EDbVendor.dbvpostgresql, EDbVendor.dbvmysql })
        {
            var parser = new TGSqlParser(vendor)
            {
                sqltext = sql
            };

            var result = parser.parse();
            if (result != 0)
                continue;

            if (parser.sqlstatements.Count == 0)
            {
                errorMessage = "Could not parse SQL query.";
                return false;
            }

            for (var i = 0; i < parser.sqlstatements.Count; i++)
            {
                var statement = parser.sqlstatements.get(i);
                if (statement.sqlstatementtype != ESqlStatementType.sstselect)
                {
                    errorMessage = "Only SELECT queries are allowed.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        errorMessage = "Could not parse SQL query.";
        return false;
    }

    private static bool ContainsBlockedFunction(string sql)
    {
        var cleaned = RemoveComments(sql);
        foreach (var functionName in BlockedFunctions)
        {
            if (BlockedFunctionPattern(functionName).IsMatch(cleaned))
                return true;
        }

        return false;
    }

    private static bool ContainsIntoClause(string sql)
    {
        var cleaned = RemoveComments(sql);
        return IntoPattern().IsMatch(cleaned);
    }

    private static bool ContainsForbiddenKeyword(string sql)
    {
        var cleaned = RemoveComments(sql);
        var matches = KeywordPattern().Matches(cleaned);

        foreach (Match match in matches)
        {
            var keyword = match.Value.ToUpperInvariant();
            if (ForbiddenKeywords.Contains(keyword))
                return true;
        }

        return false;
    }

    private static string RemoveComments(string sql)
    {
        var singleLineComments = Regex.Replace(sql, "--.*?(\r?\n|$)", " ");
        return Regex.Replace(singleLineComments, @"/\*.*?\*/", " ", RegexOptions.Singleline);
    }

    [GeneratedRegex(@"\b[A-Za-z_]\w*\b")]
    private static partial Regex KeywordPattern();

    [GeneratedRegex(@"\bINTO\s+(OUTFILE|DUMPFILE|TABLE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex IntoPattern();

    private static Regex BlockedFunctionPattern(string functionName) =>
        new($@"\b{Regex.Escape(functionName)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
