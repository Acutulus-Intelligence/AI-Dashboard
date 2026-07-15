namespace Application.DTos.Response;

public sealed record TableInfoResponse(
    string TableName,
    List<ColumnInfoResponse> Columns
);

public sealed record ColumnInfoResponse(
    string ColumnName,
    string DataType,
    bool IsNullable
);

public sealed record TablePreviewResponse(
    string TableName,
    List<ColumnInfoResponse> Columns,
    List<Dictionary<string, object?>> Rows
);
