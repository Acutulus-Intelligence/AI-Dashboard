namespace Application.DTos.Request;

public sealed record RecommendChartRequest(Guid ConnectionId, string TableName);
