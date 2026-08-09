using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalDb.Services;

public class SchemaInspector : ISchemaInspector
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ExternalDbSettings _settings;
    private readonly IConnectionAccessService _access;

    public SchemaInspector(
        AppDbContext db,
        IEncryptionService encryption,
        IOptions<ExternalDbSettings> settings,
        IConnectionAccessService access)
    {
        _db = db;
        _encryption = encryption;
        _settings = settings.Value;
        _access = access;
    }

    public async Task<List<TableSchema>> GetSchemaAsync(Guid connectionId, Guid userId, CancellationToken ct = default)
    {
        var (provider, connectionString) = await GetConnectionAsync(connectionId, userId, ct);
        var tables = new List<TableSchema>();

        using var conn = CreateConnection(provider, connectionString);
        await conn.OpenAsync(ct);

        var schemaTable = await conn.GetSchemaAsync("Tables", ct);
        foreach (System.Data.DataRow row in schemaTable.Rows)
        {
            var tableName = row["TABLE_NAME"]?.ToString();
            if (string.IsNullOrEmpty(tableName))
                continue;

            if (provider == DbProvider.SqlServer &&
                row["TABLE_TYPE"]?.ToString() != "BASE TABLE")
                continue;

            var columns = await GetColumnsAsync(conn, tableName, ct);
            tables.Add(new TableSchema { TableName = tableName, Columns = columns });
        }

        return tables;
    }

    public async Task<TableSchema> GetTableSchemaAsync(Guid connectionId, Guid userId, string tableName, CancellationToken ct = default)
    {
        var (provider, connectionString) = await GetConnectionAsync(connectionId, userId, ct);

        using var conn = CreateConnection(provider, connectionString);
        await conn.OpenAsync(ct);

        var columns = await GetColumnsAsync(conn, tableName, ct);
        return new TableSchema { TableName = tableName, Columns = columns };
    }

    public async Task<List<Dictionary<string, object?>>> PreviewTableAsync(
        Guid connectionId,
        Guid userId,
        string tableName,
        int rowLimit = 5,
        CancellationToken ct = default)
    {
        var clampedLimit = Math.Clamp(rowLimit, 1, _settings.PreviewMaxRows);
        var (provider, connectionString) = await GetConnectionAsync(connectionId, userId, ct);

        using var conn = CreateConnection(provider, connectionString);
        await conn.OpenAsync(ct);

        var schema = await GetColumnsAsync(conn, tableName, ct);
        if (schema.Count == 0)
            return [];

        var columnList = string.Join(", ", schema.Select(c => SqlIdentifierQuoter.Quote(provider, c.ColumnName)));
        var quotedTable = SqlIdentifierQuoter.Quote(provider, tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = provider == DbProvider.SqlServer
            ? $"SELECT TOP ({clampedLimit}) {columnList} FROM {quotedTable}"
            : $"SELECT {columnList} FROM {quotedTable} LIMIT {clampedLimit}";
        cmd.CommandTimeout = 15;

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

    private async Task<(DbProvider provider, string connectionString)> GetConnectionAsync(Guid connectionId, Guid userId, CancellationToken ct)
    {
        var connection = await _access.FindViewableAsync(connectionId, userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        await _db.Database.CloseConnectionAsync();

        return (connection.DbProvider, _encryption.Decrypt(connection.EncryptedConnectionString));
    }

    private static async Task<List<ColumnSchema>> GetColumnsAsync(
        System.Data.Common.DbConnection conn,
        string tableName,
        CancellationToken ct)
    {
        var columns = new List<ColumnSchema>();

        var columnTable = await conn.GetSchemaAsync("Columns", new[] { null, null, tableName, null }, ct);
        foreach (System.Data.DataRow row in columnTable.Rows)
        {
            columns.Add(new ColumnSchema
            {
                ColumnName = row["COLUMN_NAME"]?.ToString() ?? "",
                DataType = row["DATA_TYPE"]?.ToString() ?? "",
                IsNullable = row["IS_NULLABLE"]?.ToString() == "YES"
            });
        }

        return columns;
    }

    private static System.Data.Common.DbConnection CreateConnection(DbProvider provider, string connectionString)
    {
        return provider switch
        {
            DbProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySql.Data.MySqlClient.MySqlConnection(connectionString),
            DbProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }
}