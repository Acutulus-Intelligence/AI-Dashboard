namespace Application.DTos.Response;

public sealed record DatasetResponse(
    Guid Id,
    string Name,
    int ColumnCount,
    int RowCount,
    DateTime CreatedAt
);