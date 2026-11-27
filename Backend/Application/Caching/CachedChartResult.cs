namespace Application.Caching;

public sealed record CachedChartResult(
    Guid UserId,
    Guid ChartId,
    List<Dictionary<string, object?>> Rows,
    DateTime ChartUpdatedAt,
    DateTimeOffset DataUpdatedAt,
    bool Truncated = false);
