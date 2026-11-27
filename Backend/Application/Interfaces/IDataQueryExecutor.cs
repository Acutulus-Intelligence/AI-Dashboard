using Domain.Models;

namespace Application.Interfaces;

/// <summary>
/// Applies a structured <see cref="DataQueryModel"/> (filters, grouping,
/// aggregations, ordering, limit) over uploaded in-memory rows and returns the
/// resulting rows — the collection equivalent of SQL execution.
/// </summary>
public interface IDataQueryExecutor
{
    /// <summary>True when the last execution stopped at the configured row cap.</summary>
    bool LastResultTruncated { get; }

    Task<List<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> columnTypes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        DataQueryModel model,
        CancellationToken ct = default);
}