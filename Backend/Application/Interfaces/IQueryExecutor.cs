namespace Application.Interfaces;

public interface IQueryExecutor
{
    Task<List<Dictionary<string, object?>>> ExecuteAsync(Guid connectionId, Guid userId, string sql, CancellationToken ct = default);
}
