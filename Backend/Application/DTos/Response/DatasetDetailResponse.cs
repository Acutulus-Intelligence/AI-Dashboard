namespace Application.DTos.Response;

public sealed record DatasetDetailResponse(
    Guid Id,
    string Name,
    string TableName,
    List<DatasetColumnResponse> Columns,
    int RowCount,
    DateTime CreatedAt,
    List<Dictionary<string, object?>> PreviewRows
);