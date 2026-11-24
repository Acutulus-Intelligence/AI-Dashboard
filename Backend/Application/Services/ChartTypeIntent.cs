using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Detects whether the user named a chart type in their prompt. An explicit
/// choice is authoritative: readability lint may add legend/labels, but it must
/// not silently replace the requested type.
/// </summary>
public static partial class ChartTypeIntent
{
    [GeneratedRegex(
        @"\b(bar|column|line|area|pie|donut|doughnut|radar|radial|scatter|bubble|map|choropleth|table|kpi|combo|mixed|histogram|stapel|kolumn|linje|yta|paj|cirkel|punkt|karta|tabell|nyckeltal|kombination|kombi)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChartTypeWord();

    public static bool PromptNamesChartType(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt) && ChartTypeWord().IsMatch(prompt);
}
