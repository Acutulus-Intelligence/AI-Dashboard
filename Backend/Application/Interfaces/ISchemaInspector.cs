using Domain.Models;

namespace Application.Interfaces;

public interface ISchemaInspector
{
    Task<List<TableSchema>> GetSchemaAsync(Guid connectionId, Guid userId, CancellationToken ct = default);
    Task<TableSchema> GetTableSchemaAsync(Guid connectionId, Guid userId, string tableName, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> PreviewTableAsync(Guid connectionId, Guid userId, string tableName, int rowLimit = 5, CancellationToken ct = default);
}
