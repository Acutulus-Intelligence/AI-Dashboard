namespace Application.Caching;

public sealed record CachedChartResult(
    List<Dictionary<string, object?>> Rows,
    DateTime ChartUpdatedAt,
    DateTimeOffset DataUpdatedAt);
