namespace Infrastructure.Caching;

public sealed record ChartCacheInvalidationMessage(
    string InstanceId,
    string Kind,
    Guid? UserId,
    Guid? ChartId);
