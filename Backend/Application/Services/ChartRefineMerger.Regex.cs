using System.Text.RegularExpressions;

namespace Application.Services;

public static partial class ChartRefineMerger
{
    [GeneratedRegex(@"\bcolou?rs?\b", RegexOptions.CultureInvariant)]
    private static partial Regex ColourWordRegex();

    [GeneratedRegex(@"\bcolou?red\b", RegexOptions.CultureInvariant)]
    private static partial Regex ColouredWordRegex();

    [GeneratedRegex(@"\bcolou?r\s*\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex ColourNumberRegex();

    [GeneratedRegex(@"^colou?rs?\s*[-_]?\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColourIndexRegex();

    [GeneratedRegex(@"^var\(\s*--chart-([1-8])\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThemeTokenRegex();

    /// <summary>Whole-word style / colour / format tokens (prompt is lowercased first).</summary>
    [GeneratedRegex(
        @"\b(style|styled|stil|styling|variant|tema|theme|differently|"
        + @"cool|warm|mono|monochrome|contrast|"
        + @"lila|purple|röd|red|blå|blue|grön|green|"
        + @"orange|gul|yellow|teal|cyan|violet|"
        + @"dollar|dollars|krona|kronor|kr|usd|sek|"
        + @"procent|percent|percentage|round|"
        + @"legend|legends|teckenförklaring|forklaring|label|labels|etikett|etiketter)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex StyleWordRegex();

    /// <summary>
    /// Words that imply a data/query change. Used only to decide whether an
    /// explicitly stylistic prompt may safely keep the baseline SQL.
    /// </summary>
    [GeneratedRegex(
        @"\b(data|filter|filtrera|where|period|datum|date|month|månad|manad|year|år|week|vecka|"
        + @"day|dag|last|senaste|förra|forra|previous|top|sort|sortera|order|group|gruppera|"
        + @"sum|summa|average|avg|medel|count|antal|min|max|calculate|beräkna|berakna|"
        + @"sql|query|tabell|table|join|aggregate|aggregera)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DataWordRegex();
}
