namespace Application.Interfaces;

public interface IQueryExecutor
{
    /// <summary>True when the last execution stopped at the configured row cap.</summary>
    bool LastResultTruncated { get; }

    Task<List<Dictionary<string, object?>>> ExecuteAsync(Guid connectionId, Guid userId, string sql, CancellationToken ct = default);
}
