using Application.Interfaces;
using Infrastructure.ExternalDb;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Infrastructure.Datasets;

/// <summary>
/// Loads dataset rows into a transient in-memory SQLite database and runs a
/// validated SELECT against it, applying the same row/byte/time limits as the
/// external query executor.
/// </summary>
public class SqliteDatasetExecutor : IDatasetQueryExecutor
{
    private readonly ExternalDbSettings _settings;

    public SqliteDatasetExecutor(IOptions<ExternalDbSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string tableName,
        string sql,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync(ct);

        await LoadTableAsync(conn, columnNames, columnTypes, rows, tableName, ct);

        await using var cmd = conn.CreateCommand();
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
                throw new InvalidOperationException($"The result exceeded the maximum size of {_settings.QueryMaxBytes} bytes.");

            results.Add(row);
        }

        return results;
    }

    private static async Task LoadTableAsync(
        SqliteConnection conn,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string tableName,
        CancellationToken ct)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var columns = columnNames
            .Select((name, i) => $"{QuoteIdentifier(name)} {DeclaredType(columnTypes[i])}")
            .ToArray();

        await using (var create = conn.CreateCommand())
        {
            create.CommandText = $"CREATE TABLE {quotedTable} ({string.Join(", ", columns)})";
            await create.ExecuteNonQueryAsync(ct);
        }

        await using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {quotedTable} ({string.Join(", ", columnNames.Select(QuoteIdentifier))}) " +
                             $"VALUES ({string.Join(", ", Enumerable.Range(0, columnNames.Count).Select(i => $"@p{i}"))})";

        var parameters = new SqliteParameter[columnNames.Count];
        for (var i = 0; i < columnNames.Count; i++)
        {
            parameters[i] = insert.Parameters.Add($"@p{i}", SqliteType.Text);
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < columnNames.Count; i++)
            {
                var value = i < row.Count ? row[i] : null;
                parameters[i].Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
            }
            await insert.ExecuteNonQueryAsync(ct);
        }
    }

    private static string DeclaredType(string inferredType) => inferredType switch
    {
        "integer" => "INTEGER",
        "number" => "REAL",
        "boolean" => "BOOLEAN",
        _ => "TEXT"
    };

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static long EstimateSize(object value) => value switch
    {
        string s => s.Length * 2L,
        byte[] bytes => bytes.LongLength,
        _ => 16L
    };
}