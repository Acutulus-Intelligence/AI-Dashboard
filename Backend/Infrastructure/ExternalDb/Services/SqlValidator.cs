using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.Services;
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

    public bool IsSelectOnly(string sql, out string? errorMessage) =>
        IsSelectOnlyCore(sql, provider: null, out errorMessage);

    public bool IsSelectOnly(string sql, DbProvider dbProvider, out string? errorMessage) =>
        IsSelectOnlyCore(sql, dbProvider, out errorMessage);

    private static bool IsSelectOnlyCore(string sql, DbProvider? provider, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        // MySQL executes version comments as real SQL, so a comment could hide a
        // write from the parser. Reject the construct outright.
        if (MySqlExecutableCommentPattern().IsMatch(sql))
        {
            errorMessage = "Query contains an executable comment.";
            return false;
        }

        var masked = SqlMasking.MaskLiteralsAndComments(sql, provider);

        if (ContainsBlockedFunction(masked))
        {
            errorMessage = "Query contains a blocked function.";
            return false;
        }

        if (ContainsForbiddenConstruct(masked, out errorMessage))
            return false;

        if (!TryParseAsSelectOnly(sql, provider, out errorMessage))
            return false;

        if (ContainsForbiddenKeyword(masked))
        {
            errorMessage = "Query contains forbidden SQL statements.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryParseAsSelectOnly(
        string sql,
        DbProvider? provider,
        out string? errorMessage)
    {
        var vendors = provider switch
        {
            DbProvider.PostgreSql => new[] { EDbVendor.dbvpostgresql },
            DbProvider.MySql => new[] { EDbVendor.dbvmysql },
            DbProvider.SqlServer => new[] { EDbVendor.dbvmssql },
            _ => new[] { EDbVendor.dbvpostgresql, EDbVendor.dbvmysql, EDbVendor.dbvmssql }
        };

        foreach (var vendor in vendors)
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

    private static bool ContainsForbiddenConstruct(string maskedSql, out string? errorMessage)
    {
        if (IntoPattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden INTO clause.";
            return true;
        }

        if (ExternalRowsetPattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden external rowset function.";
            return true;
        }

        if (WaitForPattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden WAITFOR clause.";
            return true;
        }

        if (ForUpdatePattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden locking clause.";
            return true;
        }

        if (LockInShareModePattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden locking clause.";
            return true;
        }

        if (ProcedureAnalysePattern().IsMatch(maskedSql))
        {
            errorMessage = "Query contains a forbidden PROCEDURE clause.";
            return true;
        }

        errorMessage = null;
        return false;
    }

    private static bool ContainsBlockedFunction(string maskedSql)
    {
        foreach (var functionName in BlockedFunctions)
        {
            if (BlockedFunctionPattern(functionName).IsMatch(maskedSql))
                return true;
        }

        return false;
    }

    private static bool ContainsForbiddenKeyword(string maskedSql)
    {
        var matches = KeywordPattern().Matches(maskedSql);

        foreach (Match match in matches)
        {
            var keyword = match.Value.ToUpperInvariant();
            if (ForbiddenKeywords.Contains(keyword))
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"\b[A-Za-z_]\w*\b")]
    private static partial Regex KeywordPattern();

    [GeneratedRegex(@"\bINTO\b", RegexOptions.IgnoreCase)]
    private static partial Regex IntoPattern();

    [GeneratedRegex(@"\b(OPENROWSET|OPENQUERY|OPENDATASOURCE)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalRowsetPattern();

    [GeneratedRegex(@"\bWAITFOR\b", RegexOptions.IgnoreCase)]
    private static partial Regex WaitForPattern();

    [GeneratedRegex(@"\bFOR\s+UPDATE\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForUpdatePattern();

    [GeneratedRegex(@"\bLOCK\s+IN\s+SHARE\s+MODE\b", RegexOptions.IgnoreCase)]
    private static partial Regex LockInShareModePattern();

    [GeneratedRegex(@"\bPROCEDURE\s+ANALYSE\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProcedureAnalysePattern();

    [GeneratedRegex(@"/\*!")]
    private static partial Regex MySqlExecutableCommentPattern();

    private static Regex BlockedFunctionPattern(string functionName) =>
        new($@"\b{Regex.Escape(functionName)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
