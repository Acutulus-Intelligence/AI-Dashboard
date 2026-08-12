using System.Text.Json.Serialization;
using Domain.Charts;

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

    /// <summary>Structured query returned instead of SQL for uploaded data (collections).</summary>
    [JsonPropertyName("dataModel")]
    public DataQueryModel? DataModel { get; set; }

    /// <summary>Presentation the model picked; sanitized against the catalog before use.</summary>
    [JsonPropertyName("styleConfig")]
    public ChartStyleConfig? StyleConfig { get; set; }

    /// <summary>
    /// When the model returns <c>styleConfig.colors</c> as a name→colour object,
    /// held until <see cref="YAxis"/> (or baseline yAxis) is known for expansion.
    /// Not part of the API wire format.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string>? NamedColorMap { get; set; }
}
