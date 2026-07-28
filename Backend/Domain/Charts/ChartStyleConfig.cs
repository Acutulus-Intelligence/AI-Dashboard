using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Charts;

/// <summary>
/// Per-chart presentation settings, persisted as jsonb so new parameters can be
/// added to <see cref="ChartCatalog"/> without a schema migration.
/// </summary>
public sealed class ChartStyleConfig
{
    [JsonPropertyName("variant")]
    public string? Variant { get; set; }

    [JsonPropertyName("palette")]
    public string? Palette { get; set; }

    /// <summary>Per-series colour overrides; empty means follow the palette.</summary>
    [JsonPropertyName("colors")]
    public List<string>? Colors { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement>? Params { get; set; }

    /// <summary>Optional prefix shown on numeric values (e.g. "$").</summary>
    [JsonPropertyName("valuePrefix")]
    public string? ValuePrefix { get; set; }

    /// <summary>Optional suffix shown on numeric values (e.g. "%").</summary>
    [JsonPropertyName("valueSuffix")]
    public string? ValueSuffix { get; set; }

    /// <summary>Short info text shown in a hover tooltip next to the chart title.</summary>
    [JsonPropertyName("info")]
    public string? Info { get; set; }

    /// <summary>
    /// User-defined hex swatches available in the colour picker (in addition to theme tokens).
    /// </summary>
    [JsonPropertyName("customColors")]
    public List<string>? CustomColors { get; set; }

    /// <summary>
    /// Fixed decimal places for numeric labels. Null means show the full value.
    /// </summary>
    [JsonPropertyName("decimals")]
    public int? Decimals { get; set; }

    /// <summary>
    /// How to apply <see cref="Decimals"/>: <c>round</c> or <c>truncate</c>.
    /// Ignored when <see cref="Decimals"/> is null.
    /// </summary>
    [JsonPropertyName("decimalMode")]
    public string? DecimalMode { get; set; }
}
