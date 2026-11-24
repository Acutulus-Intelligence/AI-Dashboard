using Domain.Enums;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Infrastructure.ExternalDb.Services;

/// <summary>
/// Reads connection string components with the provider's own builder so quoted
/// values, semicolons inside passwords and SQL Server named instances are handled
/// correctly. Falls back to URI parsing for URL-style strings.
/// </summary>
internal static class ConnectionStringParts
{
    public static bool TryRead(
        DbProvider provider,
        string connectionString,
        out string host,
        out int port,
        out string database,
        out string username,
        out string? password)
    {
        host = string.Empty;
        port = 0;
        database = string.Empty;
        username = string.Empty;
        password = null;

        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        if (connectionString.Contains("://", StringComparison.Ordinal))
        {
            if (!ConnectionStringParser.TryParse(connectionString, out var parsed, out _))
                return false;

            host = parsed.Host;
            port = parsed.Port;
            database = parsed.Database;
            username = parsed.Username;
            password = parsed.Password;
            return true;
        }

        try
        {
            switch (provider)
            {
                case DbProvider.PostgreSql:
                {
                    var builder = new NpgsqlConnectionStringBuilder(connectionString);
                    host = builder.Host ?? string.Empty;
                    port = builder.Port;
                    database = builder.Database ?? string.Empty;
                    username = builder.Username ?? string.Empty;
                    password = builder.Password;
                    return true;
                }

                case DbProvider.MySql:
                {
                    var builder = new MySqlConnectionStringBuilder(connectionString);
                    host = builder.Server ?? string.Empty;
                    port = (int)builder.Port;
                    database = builder.Database ?? string.Empty;
                    username = builder.UserID ?? string.Empty;
                    password = builder.Password;
                    return true;
                }

                case DbProvider.SqlServer:
                {
                    var builder = new SqlConnectionStringBuilder(connectionString);
                    var dataSource = builder.DataSource ?? string.Empty;
                    var hostPort = dataSource.Split(',', 2);

                    host = hostPort[0].Trim();
                    if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                        host = host[4..].Trim();

                    port = hostPort.Length == 2 && int.TryParse(hostPort[1], out var parsedPort)
                        ? parsedPort
                        : 0;
                    database = builder.InitialCatalog ?? string.Empty;
                    username = builder.UserID ?? string.Empty;
                    password = builder.Password;
                    return true;
                }

                default:
                    return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
