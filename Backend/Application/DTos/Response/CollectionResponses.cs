using Domain.Enums;

namespace Application.DTos.Response;

public sealed record CollectionResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? CompanyId,
    Guid CreatedById,
    CollectionVisibility Visibility,
    List<Guid> AllowedRoleIds,
    int FileCount,
    int RowCount,
    DateTime CreatedAt
);

public sealed record CollectionFileResponse(
    Guid Id,
    string Name,
    string TableName,
    int ColumnCount,
    int RowCount,
    DateTime CreatedAt
);

public sealed record CollectionDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? CompanyId,
    Guid CreatedById,
    CollectionVisibility Visibility,
    List<Guid> AllowedRoleIds,
    DateTime CreatedAt,
    List<CollectionFileResponse> Files
);

public sealed record CollectionFileDetailResponse(
    Guid Id,
    string Name,
    string TableName,
    List<DatasetColumnResponse> Columns,
    int RowCount,
    DateTime CreatedAt,
    List<Dictionary<string, object?>> PreviewRows
);