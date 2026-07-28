using System.Text.RegularExpressions;

namespace Domain.Charts;

/// <summary>
/// Sanitizes company brand colours so only safe hex / theme tokens reach the DB.
/// </summary>
public static partial class CompanyStyleSanitizer
{
    private const int MaxColors = 24;

    /// <summary>Default swatches when a company has not customized style yet.</summary>
    public static IReadOnlyList<string> DefaultColors { get; } =
        Enumerable.Range(1, 8).Select(i => $"var(--chart-{i})").ToList();

    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|var\(--chart-[1-8]\))$")]
    private static partial Regex SafeColorPattern();

    /// <summary>
    /// Returns a non-empty colour list for API responses. Unsaved / empty configs
    /// fall back to <see cref="DefaultColors"/>.
    /// </summary>
    public static List<string> ResolveColors(CompanyStyleConfig? config)
    {
        var sanitized = SanitizeColors(config?.Colors);
        return sanitized is { Count: > 0 } ? sanitized : [.. DefaultColors];
    }

    /// <summary>
    /// Validates and normalizes a client payload. Returns null when nothing valid remains
    /// (caller should treat that as "clear to defaults" or reject).
    /// </summary>
    public static CompanyStyleConfig? Sanitize(CompanyStyleConfig? config)
    {
        if (config is null) return null;

        var colors = SanitizeColors(config.Colors);
        if (colors is null) return null;

        return new CompanyStyleConfig { Colors = colors };
    }

    private static List<string>? SanitizeColors(List<string>? colors)
    {
        if (colors is null || colors.Count == 0) return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safe = new List<string>();

        foreach (var raw in colors.Take(MaxColors))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            if (!SafeColorPattern().IsMatch(trimmed)) continue;

            var normalized = NormalizeColor(trimmed);
            if (seen.Add(normalized)) safe.Add(normalized);
        }

        return safe.Count > 0 ? safe : null;
    }

    private static string NormalizeColor(string color)
    {
        if (!color.StartsWith("#", StringComparison.Ordinal)) return color.ToLowerInvariant();

        if (color.Length is 4 or 5)
            return $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}".ToLowerInvariant();

        return (color.Length >= 7 ? color[..7] : color).ToLowerInvariant();
    }
}
