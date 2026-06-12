using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ExternalDb.Services;

public class QueryExecutor : IQueryExecutor
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ISqlValidator _validator;

    public QueryExecutor(AppDbContext db, IEncryptionService encryption, ISqlValidator validator)
    {
        _db = db;
        _encryption = encryption;
        _validator = validator;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(Guid connectionId, Guid userId, string sql, CancellationToken ct = default)
    {
        if (!_validator.IsSelectOnly(sql, out var errorMessage))
            throw new InvalidOperationException($"Query rejected: {errorMessage}");

        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == connectionId && ec.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        var connectionString = _encryption.Decrypt(connection.EncryptedConnectionString);

        using var conn = CreateConnection(connection.DbProvider, connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        var results = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[reader.GetName(i)] = value == DBNull.Value ? null : value;
            }
            results.Add(row);
        }

        return results;
    }

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
