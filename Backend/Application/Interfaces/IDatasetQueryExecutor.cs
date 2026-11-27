namespace Application.Interfaces;

/// <summary>Runs a validated SELECT against uploaded, in-memory dataset rows.</summary>
public interface IDatasetQueryExecutor
{
    /// <summary>True when the last execution stopped at the configured row cap.</summary>
    bool LastResultTruncated { get; }

    Task<List<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string tableName,
        string sql,
        CancellationToken ct = default);
}