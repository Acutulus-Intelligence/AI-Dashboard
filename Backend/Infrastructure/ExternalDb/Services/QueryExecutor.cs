using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalDb.Services;

public class QueryExecutor : IQueryExecutor
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ISqlValidator _validator;
    private readonly ExternalDbSettings _settings;

    public QueryExecutor(
        AppDbContext db,
        IEncryptionService encryption,
        ISqlValidator validator,
        IOptions<ExternalDbSettings> settings)
    {
        _db = db;
        _encryption = encryption;
        _validator = validator;
        _settings = settings.Value;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(Guid connectionId, Guid userId, string sql, CancellationToken ct = default)
    {
        if (!_validator.IsSelectOnly(sql, out var errorMessage))
            throw new InvalidOperationException($"Query rejected: {errorMessage}");

        var connection = await _db.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.Id == connectionId && ec.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        var connectionString = _encryption.Decrypt(connection.EncryptedConnectionString);
        var provider = connection.DbProvider;

        // Release the EF connection before opening an external PostgreSQL connection.
        // When both target the same server, Npgsql pooling otherwise throws
        // NpgsqlOperationInProgressException on transaction commit.
        await _db.Database.CloseConnectionAsync();

        await using var conn = CreateConnection(provider, connectionString);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);
        await SetReadOnlyAsync(conn, provider, tx, ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.CommandTimeout = _settings.QueryTimeoutSeconds;

        var results = new List<Dictionary<string, object?>>();
        long totalBytes = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (results.Count >= _settings.QueryMaxRows)
                throw new InvalidOperationException($"Query exceeded the maximum row limit of {_settings.QueryMaxRows}.");

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
        await tx.CommitAsync(ct);
        return results;
    }

    private static async Task SetReadOnlyAsync(
        System.Data.Common.DbConnection conn,
        DbProvider provider,
        System.Data.Common.DbTransaction tx,
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
            timeoutCmd.CommandText = "SET LOCAL statement_timeout = '30s'";
            await timeoutCmd.ExecuteNonQueryAsync(ct);
            return;
        }

        if (provider == DbProvider.MySql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SET SESSION TRANSACTION READ ONLY";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static long EstimateSize(object value) => value switch
    {
        string s => s.Length * 2L,
        byte[] bytes => bytes.LongLength,
        _ => 32L
    };

    private static System.Data.Common.DbConnection CreateConnection(DbProvider provider, string connectionString)
    {
        return provider switch
        {
            DbProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySql.Data.MySqlClient.MySqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }
}
