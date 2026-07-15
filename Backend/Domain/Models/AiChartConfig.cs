using System.Text.Json.Serialization;

namespace Domain.Models;

public class AiChartConfig
{
    [JsonPropertyName("chartType")]
    public string ChartType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("xAxis")]
    public string XAxis { get; set; } = string.Empty;

    [JsonPropertyName("yAxis")]
    public List<string> YAxis { get; set; } = [];

    [JsonPropertyName("aggregation")]
    public string Aggregation { get; set; } = "none";

    [JsonPropertyName("groupBy")]
    public string? GroupBy { get; set; }

    [JsonPropertyName("sqlQuery")]
    public string SqlQuery { get; set; } = string.Empty;
}
