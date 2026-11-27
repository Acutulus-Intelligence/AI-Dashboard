using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalDb.Services;

public class QueryExecutor : IQueryExecutor
{
    public bool LastResultTruncated { get; private set; }

    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ISqlValidator _validator;
    private readonly ExternalDbSettings _settings;
    private readonly IConnectionAccessService _access;
    private readonly ConnectionHostGuard _hostGuard;

    public QueryExecutor(
        AppDbContext db,
        IEncryptionService encryption,
        ISqlValidator validator,
        IOptions<ExternalDbSettings> settings,
        IConnectionAccessService access,
        ConnectionHostGuard hostGuard)
    {
        _db = db;
        _encryption = encryption;
        _validator = validator;
        _settings = settings.Value;
        _access = access;
        _hostGuard = hostGuard;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(Guid connectionId, Guid userId, string sql, CancellationToken ct = default)
    {
        LastResultTruncated = false;

        var connection = await _access.FindViewableAsync(connectionId, userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        var connectionString = _encryption.Decrypt(connection.EncryptedConnectionString);
        var provider = connection.DbProvider;

        if (!_validator.IsSelectOnly(sql, provider, out var errorMessage))
            throw new InvalidOperationException($"Query rejected: {errorMessage}");

        await _hostGuard.EnsureSafeAsync(provider, connectionString, ct);

        // Release the EF connection before opening an external PostgreSQL connection.
        // When both target the same server, Npgsql pooling otherwise throws
        // NpgsqlOperationInProgressException on transaction commit.
        await _db.Database.CloseConnectionAsync();

        await using var conn = CreateConnection(provider, connectionString);
        await conn.OpenAsync(ct);

        // MySQL rejects "SET TRANSACTION READ ONLY" once a transaction is active,
        // so start the transaction itself read-only there. PostgreSQL gets an
        // explicit read-only transaction; SQL Server has no equivalent and relies
        // on ApplicationIntent=ReadOnly plus the SELECT-only validator.
        System.Data.Common.DbTransaction? transaction = null;
        if (provider == DbProvider.MySql)
        {
            await using var begin = conn.CreateCommand();
            begin.CommandText = "START TRANSACTION READ ONLY";
            await begin.ExecuteNonQueryAsync(ct);
        }
        else
        {
            transaction = await conn.BeginTransactionAsync(ct);
            await SetReadOnlyAsync(conn, provider, transaction, _settings.QueryTimeoutSeconds, ct);
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.CommandTimeout = _settings.QueryTimeoutSeconds;

            var results = new List<Dictionary<string, object?>>();
            long totalBytes = 0;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (results.Count >= _settings.QueryMaxRows)
                {
                    LastResultTruncated = true;
                    break;
                }

                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[reader.GetName(i)] = value == DBNull.Value ? null : value;

                    if (value is not null and not DBNull)
                        totalBytes += EstimateSize(value);
                }

                if (totalBytes > _settings.QueryMaxBytes)
                    throw new InvalidOperationException($"Query exceeded the maximum result size of {_settings.QueryMaxBytes} bytes.");

                results.Add(row);
            }

            await reader.CloseAsync();

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await using var commit = conn.CreateCommand();
                commit.CommandText = "COMMIT";
                await commit.ExecuteNonQueryAsync(ct);
            }

            return results;
        }
        catch
        {
            // MySQL is driven through a manual START TRANSACTION; roll it back
            // explicitly so a failed query cannot leak an open transaction.
            if (transaction is null)
            {
                try
                {
                    await using var rollback = conn.CreateCommand();
                    rollback.CommandText = "ROLLBACK";
                    await rollback.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch
                {
                    // The connection may already be broken; disposal cleans up.
                }
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static async Task SetReadOnlyAsync(
        System.Data.Common.DbConnection conn,
        DbProvider provider,
        System.Data.Common.DbTransaction tx,
        int timeoutSeconds,
        CancellationToken ct)
    {
        if (provider == DbProvider.PostgreSql)
        {
            await using var readOnlyCmd = conn.CreateCommand();
            readOnlyCmd.Transaction = tx;
            readOnlyCmd.CommandText = "SET TRANSACTION READ ONLY";
            await readOnlyCmd.ExecuteNonQueryAsync(ct);

            await using var timeoutCmd = conn.CreateCommand();
            timeoutCmd.Transaction = tx;
            timeoutCmd.CommandText =
                $"SET LOCAL statement_timeout = '{Math.Max(1, timeoutSeconds) * 1000}'";
            await timeoutCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static long EstimateSize(object value) => value switch
    {
        string s => s.Length * 2L,
        byte[] bytes => bytes.LongLength,
        _ => 32L
    };

    private System.Data.Common.DbConnection CreateConnection(DbProvider provider, string connectionString)
    {
        return provider switch
        {
            DbProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySql.Data.MySqlClient.MySqlConnection(connectionString),
            DbProvider.SqlServer => CreateSqlServerConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    /// <summary>
    /// SQL Server has no read-only transaction. Read-only intent is an opt-in
    /// routing hint for availability groups; it can fail against servers without
    /// read-only routing, so it stays off by default.
    /// </summary>
    private Microsoft.Data.SqlClient.SqlConnection CreateSqlServerConnection(string connectionString)
    {
        if (!_settings.SqlServerReadOnlyIntent)
            return new Microsoft.Data.SqlClient.SqlConnection(connectionString);

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly
        };

        return new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
    }
}
