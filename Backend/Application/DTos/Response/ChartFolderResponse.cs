namespace Application.DTos.Response;

public sealed record ChartFolderResponse(
    Guid Id,
    string Name,
    int ChartCount,
    DateTime CreatedAt
);
