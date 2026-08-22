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

    /// <summary>Whole-word style / colour / format tokens (prompt is lowercased first).</summary>
    [GeneratedRegex(
        @"\b(style|styled|stil|styling|variant|tema|theme|differently|"
        + @"cool|warm|mono|monochrome|contrast|"
        + @"lila|purple|röd|red|blå|blue|grön|green|"
        + @"orange|gul|yellow|teal|cyan|violet|"
        + @"dollar|dollars|krona|kronor|kr|usd|sek|"
        + @"procent|percent|percentage|round)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex StyleWordRegex();
}
