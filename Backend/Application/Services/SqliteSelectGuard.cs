using System.Text.RegularExpressions;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// SELECT-only guard for chart SQL that runs against the in-memory SQLite copy of
/// an uploaded dataset. SQLite-specific statements such as ATTACH, PRAGMA and
/// DROP are rejected so a saved chart cannot write files or alter the database.
/// </summary>
public static class SqliteSelectGuard
{
    private static readonly Regex SelectOrWithPattern = new(
        @"^(SELECT|WITH)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForbiddenStatementPattern = new(
        @"\b(ATTACH|DETACH|PRAGMA|VACUUM|REINDEX|ANALYZE|CREATE|INSERT|UPDATE|DELETE|DROP|ALTER)\b"
        + @"|\bREPLACE\s+INTO\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DangerousFunctionPattern = new(
        @"\b(LOAD_EXTENSION|READFILE|WRITEFILE)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSelectOnly(string sql, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        var masked = SqlMasking.MaskLiteralsAndComments(sql, DbProvider.Sqlite).Trim();
        var statement = masked.TrimEnd(';', ' ', '\t', '\r', '\n');

        if (statement.Length == 0)
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        if (statement.Contains(';', StringComparison.Ordinal))
        {
            errorMessage = "Only a single SELECT statement is allowed.";
            return false;
        }

        if (!SelectOrWithPattern.IsMatch(statement))
        {
            errorMessage = "Only SELECT queries are allowed.";
            return false;
        }

        if (ForbiddenStatementPattern.IsMatch(statement))
        {
            errorMessage = "Query contains a statement that is not allowed for dataset charts.";
            return false;
        }

        if (DangerousFunctionPattern.IsMatch(statement))
        {
            errorMessage = "Query contains a blocked function.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
