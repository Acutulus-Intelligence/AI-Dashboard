using Application.DTos.Request;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// After a refine call, prefer the AI result for data fields and AI-controlled style
/// fields (variant, info, decimals, decimalMode, prefix/suffix, and colours/palette
/// when clamped to the account allowlist). Params stay UI-controlled.
/// Palette and per-slice colours are mutually exclusive.
/// </summary>
public static class ChartRefineMerger
{
    public static AiChartConfig Apply(
        ChartBaseline baseline,
        AiChartConfig ai,
        string userPrompt,
        IReadOnlyList<string>? allowedColors = null)
    {
        var chartType = !string.IsNullOrWhiteSpace(ai.ChartType) ? ai.ChartType : baseline.ChartType;
        var chartTypeChanged = !string.Equals(chartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);
        var wantsStyle = RequestsStyleChange(userPrompt);

        var mergedStyle = wantsStyle
            ? MergeStyle(baseline.StyleConfig, ai.StyleConfig, chartType, chartTypeChanged, allowedColors)
            : PreserveBaselineStyle(baseline.StyleConfig, chartType, chartTypeChanged);

        return new AiChartConfig
        {
            Title = !string.IsNullOrWhiteSpace(ai.Title) ? ai.Title : baseline.Title,
            ChartType = chartType,
            XAxis = !string.IsNullOrWhiteSpace(ai.XAxis) ? ai.XAxis : baseline.XAxis,
            YAxis = ai.YAxis is { Count: > 0 } ? [.. ai.YAxis] : [.. baseline.YAxis],
            Aggregation = !string.IsNullOrWhiteSpace(ai.Aggregation) ? ai.Aggregation : baseline.Aggregation,
            GroupBy = ai.GroupBy ?? baseline.GroupBy,
            SqlQuery = !string.IsNullOrWhiteSpace(ai.SqlQuery) ? ai.SqlQuery : baseline.SqlQuery,
            StyleConfig = mergedStyle,
        };
    }

    /// <summary>
    /// True when the user explicitly asked to change colours, palette, labels, variant, etc.
    /// Data-only refine prompts must leave style untouched.
    /// </summary>
    public static bool RequestsStyleChange(string? userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt)) return false;
        var p = userPrompt.ToLowerInvariant();

        // Multi-word / distinctive phrases first
        if (p.Contains("colour ", StringComparison.Ordinal)
            || p.Contains("color ", StringComparison.Ordinal)
            || p.Contains("färg", StringComparison.Ordinal)
            || p.Contains("palette", StringComparison.Ordinal)
            || p.Contains("palett", StringComparison.Ordinal)
            || p.Contains("prefix", StringComparison.Ordinal)
            || p.Contains("suffix", StringComparison.Ordinal)
            || p.Contains("decimal", StringComparison.Ordinal)
            || p.Contains("avrunda", StringComparison.Ordinal)
            || p.Contains("heltal", StringComparison.Ordinal)
            || p.Contains("truncate", StringComparison.Ordinal)
            || p.Contains("styling", StringComparison.Ordinal)
            || p.Contains("stacked", StringComparison.Ordinal)
            || p.Contains("horizontal", StringComparison.Ordinal)
            || p.Contains("grouperad", StringComparison.Ordinal)
            || p.Contains("valueprefix", StringComparison.Ordinal)
            || p.Contains("valuesuffix", StringComparison.Ordinal)
            || p.Contains("info text", StringComparison.Ordinal)
            || p.Contains("info tooltip", StringComparison.Ordinal))
            return true;

        // Whole-word tokens (avoid matching "red" inside "ordered", etc. is hard — use boundaries)
        string[] wordTokens =
        [
            "style", "stil", "variant", "tema", "theme",
            "cool", "warm", "mono", "contrast",
            "lila", "purple", "röd", "red", "blå", "blue", "grön", "green",
            "orange", "gul", "yellow", "teal", "cyan", "violet",
            "dollar", "krona", "procent", "percent", "round",
        ];

        foreach (var token in wordTokens)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    p,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(token)}\b",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                return true;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(
            p,
            @"\bcolou?r\s*\d+\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Style fields the AI is allowed to set. Params are stripped. Colours are kept
    /// only when present in <paramref name="allowedColors"/>. Palette XOR colours.
    /// Unsupported fields for the chart type are cleared.
    /// </summary>
    public static ChartStyleConfig? TakeAiControlledStyleFields(
        ChartStyleConfig? ai,
        string chartType,
        IReadOnlyList<string>? allowedColors = null)
    {
        if (ai is null) return null;

        var result = new ChartStyleConfig
        {
            Variant = NormalizeVariant(chartType, ai.Variant),
            ValuePrefix = ai.ValuePrefix,
            ValueSuffix = ai.ValueSuffix,
            Info = ai.Info,
            Decimals = ai.Decimals,
            DecimalMode = NormalizeDecimalMode(ai.DecimalMode),
            Palette = NormalizePalette(ai.Palette),
            Colors = ClampColorsToAllowlist(ai.Colors, allowedColors),
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

        NormalizeColorExclusive(result);
        StripUnsupportedStyleFields(result, chartType);

        // Drop empty objects so sanitize/defaults apply cleanly.
        if (result.Variant is null
            && result.ValuePrefix is null
            && result.ValueSuffix is null
            && result.Info is null
            && result.Decimals is null
            && result.DecimalMode is null
            && result.Palette is null
            && result.Colors is null)
            return null;

        return result;
    }

    /// <summary>
    /// Clears colours / value-format fields the chart type cannot use (e.g. table).
    /// </summary>
    public static void StripUnsupportedStyleFields(ChartStyleConfig style, string chartType)
    {
        var spec = ChartCatalog.Find(chartType);
        if (spec is null) return;

        if (!spec.SupportsColors)
        {
            style.Palette = null;
            style.Colors = null;
        }

        if (!spec.SupportsValueFormat)
        {
            style.ValuePrefix = null;
            style.ValueSuffix = null;
            style.Decimals = null;
            style.DecimalMode = null;
        }
    }

    private static ChartStyleConfig? PreserveBaselineStyle(
        ChartStyleConfig? baseline,
        string chartType,
        bool chartTypeChanged)
    {
        if (baseline is null) return null;

        var result = new ChartStyleConfig
        {
            Variant = chartTypeChanged ? null : baseline.Variant,
            ValuePrefix = baseline.ValuePrefix,
            ValueSuffix = baseline.ValueSuffix,
            Info = baseline.Info,
            Decimals = baseline.Decimals,
            DecimalMode = baseline.DecimalMode,
            Palette = baseline.Palette,
            Colors = baseline.Colors is null ? null : [.. baseline.Colors],
            CustomColors = baseline.CustomColors is null ? null : [.. baseline.CustomColors],
            Params = chartTypeChanged ? null : baseline.Params,
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

        NormalizeColorExclusive(result);
        StripUnsupportedStyleFields(result, chartType);
        return result;
    }

    /// <summary>
    /// Baseline style sent to the model — includes colours so refine can preserve/change them;
    /// omits params noise. Palette XOR colours.
    /// </summary>
    public static ChartStyleConfig? SlimStyleForAi(ChartStyleConfig? style)
    {
        if (style is null) return null;

        var slim = new ChartStyleConfig
        {
            Variant = style.Variant,
            ValuePrefix = style.ValuePrefix,
            ValueSuffix = style.ValueSuffix,
            Info = style.Info,
            Decimals = style.Decimals,
            DecimalMode = style.DecimalMode,
            Palette = style.Palette,
            Colors = style.Colors is null ? null : [.. style.Colors],
        };

        NormalizeColorExclusive(slim);

        if (slim.Variant is null
            && slim.ValuePrefix is null
            && slim.ValueSuffix is null
            && slim.Info is null
            && slim.Decimals is null
            && slim.DecimalMode is null
            && slim.Palette is null
            && slim.Colors is null)
            return null;

        return slim;
    }

    /// <summary>
    /// Theme palette and per-slice colours cannot both be active. When both are set,
    /// palette wins and colours are cleared (matches StylePanel behaviour).
    /// </summary>
    public static void NormalizeColorExclusive(ChartStyleConfig style)
    {
        if (!string.IsNullOrWhiteSpace(style.Palette))
        {
            style.Colors = null;
            return;
        }

        if (style.Colors is { Count: > 0 })
            style.Palette = null;
    }

    /// <summary>
    /// Maps a name→colour object onto a positional colours array using series keys
    /// (typically <c>yAxis</c>). Unknown keys are ignored; missing keys leave empty slots.
    /// </summary>
    public static List<string>? ExpandNamedColorMap(
        IReadOnlyDictionary<string, string> named,
        IReadOnlyList<string> seriesKeys)
    {
        if (named.Count == 0) return null;

        if (seriesKeys.Count == 0)
        {
            var values = named.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();
            return values.Count > 0 ? values : null;
        }

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in named)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
            lookup[key.Trim()] = value.Trim();
        }

        var list = new List<string>(seriesKeys.Count);
        foreach (var key in seriesKeys)
        {
            if (!string.IsNullOrWhiteSpace(key) && lookup.TryGetValue(key.Trim(), out var color))
                list.Add(color);
            else
                list.Add(string.Empty);
        }

        while (list.Count > 0 && list[^1].Length == 0)
            list.RemoveAt(list.Count - 1);

        return list.Exists(c => c.Length > 0) ? list : null;
    }

    /// <summary>
    /// Applies a pending named colour map onto <paramref name="config"/> using its yAxis
    /// as series order. Clears palette (slice mode).
    /// </summary>
    public static void ApplyNamedColorMap(AiChartConfig config, IReadOnlyDictionary<string, string>? named)
    {
        if (named is null || named.Count == 0) return;

        var expanded = ExpandNamedColorMap(named, config.YAxis);
        if (expanded is null) return;

        config.StyleConfig ??= new ChartStyleConfig();
        config.StyleConfig.Colors = expanded;
        config.StyleConfig.Palette = null;
    }

    /// <summary>
    /// Keeps only colours that appear in the account allowlist (case-insensitive).
    /// Returns null when nothing valid remains.
    /// </summary>
    public static List<string>? ClampColorsToAllowlist(
        IEnumerable<string>? colors,
        IReadOnlyList<string>? allowedColors)
    {
        if (colors is null || allowedColors is null || allowedColors.Count == 0)
            return null;

        var clamped = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in colors)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            var match = allowedColors.FirstOrDefault(a =>
                a.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            if (!seen.Add(match)) continue;
            clamped.Add(match);
        }

        return clamped.Count > 0 ? clamped : null;
    }

    private static ChartStyleConfig? MergeStyle(
        ChartStyleConfig? baseline,
        ChartStyleConfig? ai,
        string chartType,
        bool chartTypeChanged,
        IReadOnlyList<string>? allowedColors)
    {
        if (ai is null && baseline is null) return null;

        var aiFields = TakeAiControlledStyleFields(ai, chartType, allowedColors);
        var aiSetPalette = !string.IsNullOrWhiteSpace(aiFields?.Palette);
        var aiSetColors = aiFields?.Colors is { Count: > 0 };

        string? palette;
        List<string>? colors;

        if (aiSetPalette)
        {
            // Theme palette mode — drop any slice colours (including baseline).
            palette = aiFields!.Palette;
            colors = null;
        }
        else if (aiSetColors)
        {
            // Slice colour mode — drop palette.
            palette = null;
            colors = [.. aiFields!.Colors!];
        }
        else
        {
            // Neither from AI — keep baseline (already XOR if saved correctly).
            palette = baseline?.Palette;
            colors = baseline?.Colors is null ? null : [.. baseline.Colors];
        }

        var result = new ChartStyleConfig
        {
            // AI-controlled (fill gaps from baseline — except variant when type changed)
            Variant = aiFields?.Variant
                ?? (chartTypeChanged ? null : baseline?.Variant),
            ValuePrefix = aiFields?.ValuePrefix ?? baseline?.ValuePrefix,
            ValueSuffix = aiFields?.ValueSuffix ?? baseline?.ValueSuffix,
            Info = aiFields?.Info ?? baseline?.Info,
            Decimals = aiFields?.Decimals ?? baseline?.Decimals,
            DecimalMode = aiFields?.DecimalMode ?? baseline?.DecimalMode,
            Palette = palette,
            Colors = colors,

            // Always UI — never from AI
            CustomColors = baseline?.CustomColors is null ? null : [.. baseline.CustomColors],
            // Params are per chart-type; drop them on type switch so UI defaults apply
            Params = chartTypeChanged ? null : baseline?.Params,
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

        NormalizeColorExclusive(result);
        StripUnsupportedStyleFields(result, chartType);
        return result;
    }

    /// <summary>
    /// Maps AI/user labels like "grouped" onto catalog variant ids (e.g. bar → default).
    /// </summary>
    public static string? NormalizeVariant(string chartType, string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant)) return null;
        var spec = ChartCatalog.Find(chartType);
        if (spec is null) return null;

        var trimmed = variant.Trim();
        var byId = spec.Variants.FirstOrDefault(v =>
            v.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byId is not null) return byId.Id;

        var byLabel = spec.Variants.FirstOrDefault(v =>
            v.Label.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byLabel is not null) return byLabel.Id;

        if (trimmed.Equals("grouped", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("group", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("grouperad", StringComparison.OrdinalIgnoreCase))
        {
            return spec.Variants.FirstOrDefault(v => v.Id == "default")?.Id
                ?? spec.Variants.FirstOrDefault()?.Id;
        }

        return null;
    }

    private static string? NormalizePalette(string? palette)
    {
        if (string.IsNullOrWhiteSpace(palette)) return null;
        var trimmed = palette.Trim();
        var match = ChartCatalog.Palettes.FirstOrDefault(p =>
            p.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private static string? NormalizeDecimalMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        var trimmed = mode.Trim().ToLowerInvariant();
        return trimmed is "round" or "truncate" ? trimmed : null;
    }
}
