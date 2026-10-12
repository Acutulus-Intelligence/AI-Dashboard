namespace Application.DTos.Response;

public sealed record TransferDataSourceItem(string Type, Guid Id, string Name);

public sealed record TransferDashboardResponse(
    bool Transferred,
    IReadOnlyList<TransferDataSourceItem> RequiresSharing
);
