using Domain.Enums;

namespace Infrastructure.ExternalDb.Services;

public static class SqlIdentifierQuoter
{
    public static string Quote(DbProvider provider, string identifier)
    {
        return provider switch
        {
            DbProvider.PostgreSql => QuoteDoubleQuoted(identifier),
            DbProvider.MySql => QuoteBacktick(identifier),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    public static string GetQuotingRule(DbProvider provider) => provider switch
    {
        DbProvider.PostgreSql => "Use double quotes for table and column names (e.g. \"table_name\")",
        DbProvider.MySql => "Use backticks for table and column names (e.g. `table_name`)",
        _ => "Quote identifiers appropriately for the database"
    };

    private static string QuoteDoubleQuoted(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static string QuoteBacktick(string identifier) =>
        $"`{identifier.Replace("`", "``")}`";
}
