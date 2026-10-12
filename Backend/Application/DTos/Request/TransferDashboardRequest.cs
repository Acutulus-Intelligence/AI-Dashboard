namespace Application.DTos.Request;

public sealed record TransferDashboardRequest(Guid NewOwnerId, string CurrentPassword, bool ShareDataSources = false);
