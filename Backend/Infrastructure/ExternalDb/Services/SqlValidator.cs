using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Infrastructure.ExternalDb.Services;

public partial class SqlValidator : ISqlValidator
{
    private static readonly Regex FirstKeywordRegex = FirstKeywordPattern();

    private static readonly HashSet<string> ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "CREATE", "EXEC", "EXECUTE", "MERGE", "CALL", "REPLACE",
        "GRANT", "REVOKE", "RENAME", "LOAD", "IMPORT"
    ];

    public bool IsSelectOnly(string sql, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            errorMessage = "SQL query is empty.";
            return false;
        }

        var firstKeyword = ExtractFirstKeyword(sql);

        if (firstKeyword is null)
        {
            errorMessage = "Could not parse SQL query.";
            return false;
        }

        if (firstKeyword != "SELECT" && firstKeyword != "WITH")
        {
            errorMessage = $"Only SELECT queries are allowed. Found: '{firstKeyword}'.";
            return false;
        }

        if (firstKeyword == "WITH")
        {
            if (!ContainsSelectOnly(sql))
            {
                errorMessage = "WITH queries may only contain SELECT statements.";
                return false;
            }
        }

        if (ContainsForbiddenKeyword(sql))
        {
            errorMessage = "Query contains forbidden SQL statements.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static string? ExtractFirstKeyword(string sql)
    {
        var trimmed = RemoveComments(sql).Trim();
        var match = FirstKeywordRegex.Match(trimmed);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string RemoveComments(string sql)
    {
        var singleLineComments = Regex.Replace(sql, "--.*?(\r?\n|$)", " ");
        var multiLineComments = Regex.Replace(singleLineComments, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return multiLineComments;
    }

    private static bool ContainsForbiddenKeyword(string sql)
    {
        var cleaned = RemoveComments(sql);
        var matches = FirstKeywordRegex.Matches(cleaned);

        foreach (Match match in matches)
        {
            var keyword = match.Value.ToUpperInvariant();
            if (ForbiddenKeywords.Contains(keyword))
                return true;
        }

        return false;
    }

    private static bool ContainsSelectOnly(string sql)
    {
        var cleaned = RemoveComments(sql);
        var matches = FirstKeywordRegex.Matches(cleaned);

        foreach (Match match in matches)
        {
            var keyword = match.Value.ToUpperInvariant();
            if (ForbiddenKeywords.Contains(keyword) || keyword == "WITH")
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"\b[A-Za-z_]\w*\b")]
    private static partial Regex FirstKeywordPattern();
}
