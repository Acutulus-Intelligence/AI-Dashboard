using System.Text.Json.Serialization;

namespace Domain.Charts;

/// <summary>
/// Company-wide brand preferences (expandable later beyond colours).
/// Persisted as jsonb on <see cref="Models.Company"/>.
/// </summary>
public sealed class CompanyStyleConfig
{
    /// <summary>
    /// Swatches offered in the chart colour picker (hex and/or <c>var(--chart-N)</c>).
    /// </summary>
    [JsonPropertyName("colors")]
    public List<string>? Colors { get; set; }
}
