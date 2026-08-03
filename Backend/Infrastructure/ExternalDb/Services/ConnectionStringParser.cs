using Domain.Enums;

namespace Infrastructure.ExternalDb.Services;

public sealed record ParsedConnectionString(
    DbProvider? Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password);

public static class ConnectionStringParser
{
    public static bool TryParse(string input, out ParsedConnectionString result, out string error)
    {
        result = default!;
        error = string.Empty;

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Connection string is empty.";
            return false;
        }

        if (trimmed.Contains("://"))
            return TryParseUri(trimmed, out result, out error);

        return TryParseKeyValue(trimmed, out result, out error);
    }

    private static bool TryParseUri(string input, out ParsedConnectionString result, out string error)
    {
        result = default!;
        error = string.Empty;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "The connection string is not a valid database URL.";
            return false;
        }

        var provider = uri.Scheme.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => DbProvider.PostgreSql,
            "mysql" or "mariadb" => DbProvider.MySql,
            "mssql" or "sqlserver" => DbProvider.SqlServer,
            "sqlite" or "file" => DbProvider.Sqlite,
            _ => (DbProvider?)null
        };

        if (provider is null || !IsPhaseOneProvider(provider.Value))
        {
            error = "Unsupported database scheme in connection string.";
            return false;
        }

        string username = string.Empty;
        string password = string.Empty;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length == 2)
                password = Uri.UnescapeDataString(parts[1]);
        }

        var defaultPort = provider switch
        {
            DbProvider.PostgreSql => 5432,
            DbProvider.MySql => 3306,
            DbProvider.SqlServer => 1433,
            _ => 0
        };

        var port = uri.Port > 0 ? uri.Port : defaultPort;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        result = new ParsedConnectionString(provider, uri.Host, port, database, username, password);
        return true;
    }

    private static bool TryParseKeyValue(string input, out ParsedConnectionString result, out string error)
    {
        result = default!;
        error = string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawPair in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = rawPair.Trim();
            if (pair.Length == 0)
                continue;

            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                error = $"Invalid key=value pair in connection string: \"{pair}\".";
                return false;
            }

            var key = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            values[key] = value;
        }

        string? Get(params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (values.TryGetValue(alias, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        var host = Get("host", "server", "data source", "datasource", "serveraddress") ?? "";
        var database = Get("database", "db", "initial catalog", "initialcatalog", "databasename") ?? "";
        var username = Get("username", "user id", "userid", "user", "uid") ?? "";
        var password = Get("password", "pwd") ?? "";

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Connection string is missing a host (Host, Server, or Data Source).";
            return false;
        }

        // Allow SQL Server style "Server=host,1433".
        var hostPort = host.Split(',', 2);
        host = hostPort[0].Trim();
        if (hostPort.Length == 2 && int.TryParse(hostPort[1], out var commaPort))
        {
            result = new ParsedConnectionString(null, host, commaPort, database, username, password);
            return true;
        }

        if (int.TryParse(Get("port") ?? "", out var portKey))
        {
            result = new ParsedConnectionString(null, host, portKey, database, username, password);
            return true;
        }

        result = new ParsedConnectionString(null, host, 0, database, username, password);
        return true;
    }

    // Phase 1 supports PostgreSql and MySql end-to-end. SQL Server and SQLite
    // URI schemes are recognised but gated until their implementation lands.
    private static bool IsPhaseOneProvider(DbProvider provider) =>
        provider is DbProvider.PostgreSql or DbProvider.MySql;
}