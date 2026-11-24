using Domain.Enums;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Infrastructure.ExternalDb.Services;

/// <summary>
/// Validates and normalizes connection strings before they are stored. Dangerous
/// driver options (Windows authentication, attached files, local infile, disabled
/// TLS) are rejected, and the string is reserialized through the provider builder
/// so unknown keys cannot reach the driver.
///
/// URI-style strings are converted to the provider's key/value format so TLS can
/// be enforced and the driver receives a form it understands.
/// </summary>
public static class ConnectionStringSecurity
{
    public static bool TryNormalize(
        string input,
        DbProvider provider,
        bool requireTls,
        out string normalized,
        out string? error,
        bool verifyServerCertificate = true)
    {
        normalized = input;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Connection string is empty.";
            return false;
        }

        var trimmed = input.Trim();

        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            return TryNormalizeUri(
                trimmed, provider, requireTls, verifyServerCertificate, out normalized, out error);
        }

        var values = ParseKeyValues(trimmed);
        if (values is null)
        {
            error = "The connection string contains an invalid key/value pair.";
            return false;
        }

        if (TryFindInsecureSetting(values, verifyServerCertificate, out error))
            return false;

        try
        {
            switch (provider)
            {
                case DbProvider.PostgreSql:
                {
                    var builder = new NpgsqlConnectionStringBuilder(trimmed);
                    ApplyPostgresTls(builder, requireTls, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                case DbProvider.MySql:
                {
                    var builder = new MySqlConnectionStringBuilder(trimmed);
                    ApplyMySqlTls(builder, requireTls, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                case DbProvider.SqlServer:
                {
                    var builder = new SqlConnectionStringBuilder(trimmed);
                    ApplySqlServerTls(builder, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                default:
                    error = "This database provider is not supported.";
                    return false;
            }
        }
        catch (ArgumentException ex)
        {
            error = $"The connection string contains an unsupported setting: {ex.Message}";
            return false;
        }
    }

    private static bool TryNormalizeUri(
        string input,
        DbProvider provider,
        bool requireTls,
        bool verifyServerCertificate,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            error = "The connection string is not a valid database URL.";
            return false;
        }

        var schemeProvider = uri.Scheme.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => DbProvider.PostgreSql,
            "mysql" or "mariadb" => DbProvider.MySql,
            "mssql" or "sqlserver" => DbProvider.SqlServer,
            _ => (DbProvider?)null
        };

        if (schemeProvider is null)
        {
            error = "Unsupported database scheme in connection string.";
            return false;
        }

        if (schemeProvider != provider)
        {
            error = $"The connection string does not match the selected database provider ({schemeProvider}).";
            return false;
        }

        var query = ParseQuery(uri.Query);
        if (TryFindInsecureSetting(query, verifyServerCertificate, out error))
            return false;

        if (!ConnectionStringParts.TryRead(
                provider,
                input,
                out var host,
                out var port,
                out var database,
                out var username,
                out var password))
        {
            error = "The connection string is not a valid database URL.";
            return false;
        }

        try
        {
            switch (provider)
            {
                case DbProvider.PostgreSql:
                {
                    var builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = host,
                        Port = port > 0 ? port : 5432,
                        Database = database,
                        Username = username,
                        Password = password,
                    };

                    if (TryGet(query, "sslmode", out var sslMode)
                        || TryGet(query, "ssl mode", out sslMode)
                        || TryGet(query, "ssl-mode", out sslMode))
                    {
                        if (!Enum.TryParse<Npgsql.SslMode>(NormalizeEnumValue(sslMode), ignoreCase: true, out var parsedMode))
                        {
                            error = $"Unknown sslmode '{sslMode}'.";
                            return false;
                        }

                        builder.SslMode = parsedMode;
                    }

                    if (TryGet(query, "application_name", out var appName))
                        builder.ApplicationName = appName;

                    if (TryGet(query, "connect_timeout", out var timeout)
                        && int.TryParse(timeout, out var timeoutSeconds))
                    {
                        builder.Timeout = timeoutSeconds;
                    }

                    if (TryGet(query, "search_path", out var searchPath))
                        builder.SearchPath = searchPath;

                    if (TryGet(query, "options", out var options))
                        builder.Options = options;

                    if (TryGet(query, "sslrootcert", out var rootCertificate))
                        builder.RootCertificate = rootCertificate;

                    ApplyPostgresTls(builder, requireTls, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                case DbProvider.MySql:
                {
                    var builder = new MySqlConnectionStringBuilder
                    {
                        Server = host,
                        Port = (uint)(port > 0 ? port : 3306),
                        Database = database,
                        UserID = username,
                        Password = password,
                    };

                    if (TryGet(query, "sslmode", out var sslMode)
                        || TryGet(query, "ssl mode", out sslMode)
                        || TryGet(query, "ssl-mode", out sslMode))
                    {
                        var normalizedSsl = NormalizeEnumValue(sslMode);
                        if (normalizedSsl.Equals("verifyidentity", StringComparison.OrdinalIgnoreCase))
                            normalizedSsl = "verifyfull";

                        if (!Enum.TryParse<MySqlSslMode>(normalizedSsl, ignoreCase: true, out var parsedMode))
                        {
                            error = $"Unknown sslmode '{sslMode}'.";
                            return false;
                        }

                        builder.SslMode = parsedMode;
                    }

                    if ((TryGet(query, "connect_timeout", out var timeout)
                            || TryGet(query, "connect-timeout", out timeout))
                        && uint.TryParse(timeout, out var timeoutSeconds))
                    {
                        builder.ConnectionTimeout = timeoutSeconds;
                    }

                    if (TryGet(query, "allowpublickeyretrieval", out var publicKey)
                        && IsTruthy(publicKey))
                    {
                        builder.AllowPublicKeyRetrieval = true;
                    }

                    if (TryGet(query, "ssl-ca", out var sslCa)
                        || TryGet(query, "ssl_ca", out sslCa))
                    {
                        builder.SslCa = sslCa;
                    }

                    if (TryGet(query, "ssl-cert", out var sslCert)
                        || TryGet(query, "ssl_cert", out sslCert))
                    {
                        builder.SslCert = sslCert;
                    }

                    if (TryGet(query, "ssl-key", out var sslKey)
                        || TryGet(query, "ssl_key", out sslKey))
                    {
                        builder.SslKey = sslKey;
                    }

                    ApplyMySqlTls(builder, requireTls, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                case DbProvider.SqlServer:
                {
                    var builder = new SqlConnectionStringBuilder
                    {
                        DataSource = port > 0 ? $"{host},{port}" : host,
                        InitialCatalog = database,
                        UserID = username,
                        Password = password,
                    };

                    if (TryGet(query, "encrypt", out var encrypt))
                    {
                        if (encrypt.Trim().Equals("strict", StringComparison.OrdinalIgnoreCase))
                            builder.Encrypt = SqlConnectionEncryptOption.Strict;
                        else if (IsTruthy(encrypt)
                                 || encrypt.Trim().Equals("mandatory", StringComparison.OrdinalIgnoreCase))
                            builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                    }

                    if (TryGet(query, "application_name", out var appName)
                        || TryGet(query, "application name", out appName))
                    {
                        builder.ApplicationName = appName;
                    }

                    if ((TryGet(query, "connect_timeout", out var timeout)
                            || TryGet(query, "connect timeout", out timeout))
                        && int.TryParse(timeout, out var timeoutSeconds))
                    {
                        builder.ConnectTimeout = timeoutSeconds;
                    }

                    ApplySqlServerTls(builder, verifyServerCertificate);
                    normalized = builder.ConnectionString;
                    return true;
                }

                default:
                    error = "This database provider is not supported.";
                    return false;
            }
        }
        catch (ArgumentException ex)
        {
            error = $"The connection string contains an unsupported setting: {ex.Message}";
            return false;
        }
        catch (UriFormatException)
        {
            error = "The connection string is not a valid database URL.";
            return false;
        }
    }

    private static void ApplyPostgresTls(
        NpgsqlConnectionStringBuilder builder,
        bool requireTls,
        bool verifyServerCertificate)
    {
        if (!requireTls)
            return;

        if (builder.SslMode is Npgsql.SslMode.Disable
            or Npgsql.SslMode.Allow
            or Npgsql.SslMode.Prefer)
        {
            builder.SslMode = verifyServerCertificate
                ? Npgsql.SslMode.VerifyFull
                : Npgsql.SslMode.Require;
        }
        else if (verifyServerCertificate && builder.SslMode is Npgsql.SslMode.Require)
        {
            builder.SslMode = Npgsql.SslMode.VerifyFull;
        }
    }

    private static void ApplyMySqlTls(
        MySqlConnectionStringBuilder builder,
        bool requireTls,
        bool verifyServerCertificate)
    {
        if (!requireTls)
            return;

        if (builder.SslMode is MySqlSslMode.None or MySqlSslMode.Preferred)
        {
            builder.SslMode = verifyServerCertificate
                ? MySqlSslMode.VerifyFull
                : MySqlSslMode.Required;
        }
        else if (verifyServerCertificate && builder.SslMode is MySqlSslMode.Required)
        {
            builder.SslMode = MySqlSslMode.VerifyFull;
        }
    }

    private static void ApplySqlServerTls(
        SqlConnectionStringBuilder builder,
        bool verifyServerCertificate)
    {
        builder.TrustServerCertificate = !verifyServerCertificate;

        // Mandatory is the minimum; do not downgrade a caller that explicitly
        // selected Strict (TDS 8.0).
        if (builder.Encrypt != SqlConnectionEncryptOption.Strict)
            builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
    }

    private static bool TryFindInsecureSetting(
        Dictionary<string, string> values,
        bool verifyServerCertificate,
        out string? error)
    {
        error = null;

        if ((TryGet(values, "integrated security", out var integrated)
                || TryGet(values, "trusted_connection", out integrated)
                || TryGet(values, "integrated_security", out integrated))
            && !IsFalsy(integrated))
        {
            error = "Windows authentication is not allowed; use a dedicated database user.";
            return true;
        }

        if (TryGet(values, "attachdbfilename", out var attach)
            && !string.IsNullOrWhiteSpace(attach))
        {
            error = "AttachDbFilename is not allowed.";
            return true;
        }

        if (IsTrue(values, "user instance"))
        {
            error = "User Instance is not allowed.";
            return true;
        }

        if (IsTrue(values, "allowloadlocalinfile")
            || IsTrue(values, "allow load local infile")
            || IsTrue(values, "allow_load_local_infile"))
        {
            error = "AllowLoadLocalInfile is not allowed.";
            return true;
        }

        if ((TryGet(values, "allowloadlocalinfileinpath", out var localPath)
                || TryGet(values, "allow load local infile in path", out localPath)
                || TryGet(values, "allow_load_local_infile_in_path", out localPath))
            && !string.IsNullOrWhiteSpace(localPath))
        {
            error = "AllowLoadLocalInfileInPath is not allowed.";
            return true;
        }

        if (verifyServerCertificate
            && (IsTrue(values, "trustservercertificate")
                || IsTrue(values, "trust server certificate")
                || IsTrue(values, "trust_server_certificate")))
        {
            error = "TrustServerCertificate=true is not allowed; use a trusted certificate.";
            return true;
        }

        if (TryGet(values, "encrypt", out var encrypt)
            && (IsFalsy(encrypt) || IsOptionalEncryptValue(encrypt)))
        {
            error = "Encrypt=false/optional is not allowed.";
            return true;
        }

        if ((TryGet(values, "sslmode", out var sslMode)
                || TryGet(values, "ssl mode", out sslMode)
                || TryGet(values, "ssl-mode", out sslMode))
            && IsDisabledTlsValue(sslMode))
        {
            error = "Disabling TLS is not allowed.";
            return true;
        }

        return false;
    }

    private static bool IsDisabledTlsValue(string value)
    {
        var normalized = value.Trim().Trim('"').ToLowerInvariant();
        return normalized is "disable" or "disabled" or "none" or "allow" or "off" or "false" or "0";
    }

    /// <summary>
    /// Splits key/value pairs while respecting quoted values. Returns null when a
    /// pair is malformed, so storage fails closed instead of passing an ambiguous
    /// string to the driver.
    /// </summary>
    private static Dictionary<string, string>? ParseKeyValues(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var malformed = false;

        void Flush()
        {
            if (malformed)
                return;

            var pair = current.ToString().Trim();
            current.Clear();

            if (pair.Length == 0)
                return;

            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                malformed = true;
                return;
            }

            var key = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim().Trim('"');
            if (key.Length == 0)
            {
                malformed = true;
                return;
            }

            result[key] = value;
        }

        foreach (var c in input)
        {
            if (c == '"')
                inQuotes = !inQuotes;

            if (c == ';' && !inQuotes)
            {
                Flush();
                if (malformed)
                    return null;

                continue;
            }

            current.Append(c);
        }

        Flush();
        return malformed ? null : result;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return result;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = Uri.UnescapeDataString(part[..separator]);
            var value = Uri.UnescapeDataString(part[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static bool TryGet(
        Dictionary<string, string> values,
        string key,
        out string value)
    {
        if (values.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsTrue(Dictionary<string, string> values, string key) =>
        TryGet(values, key, out var value) && IsTruthy(value);

    private static bool IsOptionalEncryptValue(string value) =>
        value.Trim().Trim('"').Equals("optional", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEnumValue(string value) =>
        value.Trim().Trim('"').Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

    private static bool IsTruthy(string value)
    {
        var normalized = value.Trim().Trim('"').ToLowerInvariant();
        return normalized is "true" or "1" or "yes" or "on";
    }

    private static bool IsFalsy(string value)
    {
        var normalized = value.Trim().Trim('"').ToLowerInvariant();
        return normalized is "false" or "0" or "no" or "off";
    }
}
