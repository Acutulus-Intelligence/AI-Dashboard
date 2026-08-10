using Domain.Charts;
using Domain.Models;

namespace Application.DTos.Request;

public sealed record SaveChartRequest(
    string Title,
    string ChartType,
    string XAxis,
    List<string> YAxis,
    string Aggregation,
    string? GroupBy,
    string SqlQuery,
    Guid? ConnectionId,
    Guid? DatasetId,
    string? TableName,
    ChartStyleConfig? StyleConfig = null,
    DataQueryModel? DataModel = null
);