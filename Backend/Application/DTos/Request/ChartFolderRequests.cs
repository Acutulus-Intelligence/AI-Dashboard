namespace Application.DTos.Request;

public sealed record CreateChartFolderRequest(string Name);

public sealed record UpdateChartFolderRequest(string Name);

public sealed record MoveChartRequest(Guid? FolderId);
