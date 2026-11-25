using Application.DTos.Request;
using Domain.Charts;
using Domain.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// After a refine call, prefer the AI result for data fields and AI-controlled style
/// fields (variant, info, decimals, decimalMode, prefix/suffix, and colours/palette
/// when clamped to the account allowlist). Params stay UI-controlled.
/// Palette and per-slice colours are mutually exclusive.
/// </summary>
public static partial class ChartRefineMerger
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
            DataModel = ai.DataModel ?? baseline.DataModel,
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
        if (p.Contains("färg", StringComparison.Ordinal)
            || p.Contains("palette", StringComparison.Ordinal)
            || p.Contains("palett", StringComparison.Ordinal)
            || p.Contains("prefix", StringComparison.Ordinal)
            || p.Contains("suffix", StringComparison.Ordinal)
            || p.Contains("decimal", StringComparison.Ordinal)
            || p.Contains("avrunda", StringComparison.Ordinal)
            || p.Contains("heltal", StringComparison.Ordinal)
            || p.Contains("truncate", StringComparison.Ordinal)
            || p.Contains("stacked", StringComparison.Ordinal)
            || p.Contains("horizontal", StringComparison.Ordinal)
            || p.Contains("grouperad", StringComparison.Ordinal)
            || p.Contains("valueprefix", StringComparison.Ordinal)
            || p.Contains("valuesuffix", StringComparison.Ordinal)
            || p.Contains("info text", StringComparison.Ordinal)
            || p.Contains("info tooltip", StringComparison.Ordinal)
            || p.Contains("value label", StringComparison.Ordinal)
            || p.Contains("visa värden", StringComparison.Ordinal)
            || p.Contains("visa siffror", StringComparison.Ordinal))
            return true;

        if (ColourWordRegex().IsMatch(p)
            || ColouredWordRegex().IsMatch(p)
            || ColourNumberRegex().IsMatch(p)
            || StyleWordRegex().IsMatch(p))
            return true;

        return false;
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
            Params = FilterAiParams(ai.Params, chartType),
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
            && result.Colors is null
            && result.Params is null)
            return null;

        return result;
    }

    /// <summary>
    /// Only legend/label visibility may come from the model. Every other param
    /// key is owned by the UI and by the deterministic chart lint.
    /// </summary>
    private static readonly HashSet<string> AiParamKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "showLegend",
        "showLabels",
    };

    public static Dictionary<string, JsonElement>? FilterAiParams(
        Dictionary<string, JsonElement>? values,
        string chartType)
    {
        if (values is null || values.Count == 0) return null;

        var spec = ChartCatalog.Find(chartType);
        if (spec is null) return null;

        var result = new Dictionary<string, JsonElement>();
        foreach (var (key, raw) in values)
        {
            if (!AiParamKeys.Contains(key)) continue;

            var param = spec.Params.FirstOrDefault(p =>
                p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (param is null || param.Kind != ChartParamKind.Boolean) continue;
            if (raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) continue;

            result[param.Key] = raw;
        }

        return result.Count > 0 ? result : null;
    }

    private static Dictionary<string, JsonElement>? MergeParams(
        Dictionary<string, JsonElement>? baseline,
        Dictionary<string, JsonElement>? ai,
        string chartType,
        bool chartTypeChanged)
    {
        if (chartTypeChanged)
            return FilterAiParams(ai, chartType);

        if (ai is null || ai.Count == 0)
            return baseline;

        var merged = baseline is null
            ? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement>(baseline);
        foreach (var (key, value) in ai)
            merged[key] = value;

        return merged;
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
            style.ColorsByKey = null;
        }

        if (!spec.SupportsValueFormat)
        {
            style.ValuePrefix = null;
            style.ValueSuffix = null;
            style.Decimals = null;
            style.DecimalMode = null;
            style.Notation = null;
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
            Notation = baseline.Notation,
            Palette = baseline.Palette,
            Colors = baseline.Colors is null ? null : [.. baseline.Colors],
            ColorsByKey = baseline.ColorsByKey is null
                ? null
                : new Dictionary<string, string>(baseline.ColorsByKey),
            Params = chartTypeChanged ? null : baseline.Params,
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

        NormalizeColorExclusive(result);
        StripUnsupportedStyleFields(result, chartType);
        return result;
    }

    /// <summary>
    /// Baseline style sent to the model — includes colours and the two
    /// AI-writable params (legend/labels) so refine can preserve or change them.
    /// Palette XOR colours.
    /// </summary>
    public static ChartStyleConfig? SlimStyleForAi(ChartStyleConfig? style, string chartType)
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
            Params = FilterAiParams(style.Params, chartType),
        };

        NormalizeColorExclusive(slim);

        if (slim.Variant is null
            && slim.ValuePrefix is null
            && slim.ValueSuffix is null
            && slim.Info is null
            && slim.Decimals is null
            && slim.DecimalMode is null
            && slim.Palette is null
            && slim.Colors is null
            && slim.Params is null)
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
            // Palette is the base; name-keyed overrides still win for individual
            // categories/series. Positional overrides stay legacy-only.
            style.Colors = null;
            return;
        }

        if (style.Colors is { Count: > 0 } && HasNonEmptyColorSlot(style.Colors))
            style.Palette = null;
        else if (style.ColorsByKey is { Count: > 0 })
            style.Palette = null;
    }

    /// <summary>True when at least one series/slice has an explicit colour override.</summary>
    public static bool HasNonEmptyColorSlot(IEnumerable<string>? colors)
        => colors is not null && colors.Any(c => !string.IsNullOrWhiteSpace(c));

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
    /// Keeps only colours that appear in the account allowlist (case-insensitive).
    /// Invented values are snapped when possible: "Colour N" labels, hue words
    /// (blue/red/…), and hex close to an allowlisted swatch. Unknown values
    /// become empty slots ("follow palette") rather than leaking invalid CSS.
    /// </summary>
    public static List<string>? ClampColorsToAllowlist(
        IEnumerable<string>? colors,
        IReadOnlyList<string>? allowedColors)
    {
        if (colors is null || allowedColors is null || allowedColors.Count == 0)
            return null;

        var clamped = new List<string>();

        foreach (var raw in colors)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                clamped.Add(string.Empty);
                continue;
            }

            clamped.Add(SnapColorToAllowlist(raw.Trim(), allowedColors) ?? string.Empty);
        }

        while (clamped.Count > 0 && clamped[^1].Length == 0)
            clamped.RemoveAt(clamped.Count - 1);

        return clamped.Exists(c => c.Length > 0) ? clamped : null;
    }

    /// <summary>
    /// Maps a model colour onto the account allowlist, or null when it cannot be grounded.
    /// </summary>
    public static string? SnapColorToAllowlist(string raw, IReadOnlyList<string> allowedColors)
    {
        if (string.IsNullOrWhiteSpace(raw) || allowedColors.Count == 0)
            return null;

        var trimmed = raw.Trim();
        var exact = allowedColors.FirstOrDefault(a =>
            a.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var colourIndex = ParseColourIndex(trimmed);
        if (colourIndex is >= 1 && colourIndex <= allowedColors.Count)
            return allowedColors[colourIndex.Value - 1];

        var hue = SnapHueWord(trimmed, allowedColors);
        if (hue is not null)
            return hue;

        return SnapNearestHex(trimmed, allowedColors);
    }

    private static int? ParseColourIndex(string value)
    {
        var match = ColourIndexRegex().Match(value);
        if (!match.Success) return null;
        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }

    private static string? SnapHueWord(string value, IReadOnlyList<string> allowedColors)
    {
        if (!HueToChartIndex.TryGetValue(value, out var chartIndex))
            return null;

        var token = $"var(--chart-{chartIndex})";
        return allowedColors.FirstOrDefault(a =>
            a.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? SnapNearestHex(string value, IReadOnlyList<string> allowedColors)
    {
        if (!TryParseRgb(value, out var r, out var g, out var b))
            return null;

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var allowed in allowedColors)
        {
            if (!TryParseRgb(allowed, out var ar, out var ag, out var ab)
                && !TryThemeTokenRgb(allowed, out ar, out ag, out ab))
                continue;

            var distance = ColorDistance(r, g, b, ar, ag, ab);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = allowed;
            }
        }

        // Conservative: only snap near-miss hex, not arbitrary complementary colours.
        return bestDistance <= 48 ? best : null;
    }

    private static bool TryThemeTokenRgb(string value, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var match = ThemeTokenRegex().Match(value.Trim());
        if (!match.Success || !ThemeChartRgb.TryGetValue(match.Groups[1].Value, out var rgb))
            return false;
        (r, g, b) = rgb;
        return true;
    }

    private static bool TryParseRgb(string value, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var hex = value.Trim();
        if (!hex.StartsWith('#') || hex.Length is < 4 or > 9)
            return false;

        hex = hex[1..];
        if (hex.Length is 3 or 4)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        else if (hex.Length >= 6)
            hex = hex[..6];
        else
            return false;

        if (!int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            || !int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            || !int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            return false;

        return true;
    }

    private static int ColorDistance(int r1, int g1, int b1, int r2, int g2, int b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return (int)Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>Approximate light-theme hues for default <c>--chart-N</c> tokens.</summary>
    private static readonly Dictionary<string, (int R, int G, int B)> ThemeChartRgb = new(StringComparer.Ordinal)
    {
        ["1"] = (59, 130, 246),
        ["2"] = (249, 115, 22),
        ["3"] = (34, 197, 94),
        ["4"] = (168, 85, 247),
        ["5"] = (234, 179, 8),
        ["6"] = (239, 68, 68),
        ["7"] = (20, 184, 166),
        ["8"] = (139, 92, 246),
        ["9"] = (236, 72, 153),
        ["10"] = (132, 204, 22),
        ["11"] = (14, 165, 233),
        ["12"] = (217, 119, 6),
        ["13"] = (99, 102, 241),
        ["14"] = (244, 63, 94),
        ["15"] = (16, 185, 129),
        ["16"] = (100, 116, 139),
    };

    private static readonly Dictionary<string, int> HueToChartIndex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"] = 1, ["blå"] = 1, ["bla"] = 1,
        ["orange"] = 2,
        ["green"] = 3, ["grön"] = 3, ["gron"] = 3,
        ["purple"] = 4, ["lila"] = 4,
        ["yellow"] = 5, ["gold"] = 5, ["gul"] = 5,
        ["red"] = 6, ["röd"] = 6, ["rod"] = 6,
        ["teal"] = 7, ["cyan"] = 7,
        ["violet"] = 8,
        ["pink"] = 9, ["rosa"] = 9,
        ["lime"] = 10,
        ["sky"] = 11,
        ["amber"] = 12,
        ["indigo"] = 13,
        ["rose"] = 14,
        ["emerald"] = 15,
        ["slate"] = 16,
    };

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
        var aiSetColors = HasNonEmptyColorSlot(aiFields?.Colors);

        string? palette;
        List<string>? colors;
        Dictionary<string, string>? colorsByKey;

        if (aiSetPalette)
        {
            // Theme palette mode — drop any slice colours (including baseline).
            palette = aiFields!.Palette;
            colors = null;
            colorsByKey = null;
        }
        else if (aiSetColors)
        {
            // Slice colour mode — drop palette.
            palette = null;
            colors = [.. aiFields!.Colors!];
            colorsByKey = null;
        }
        else
        {
            // Neither from AI — keep baseline (already XOR if saved correctly).
            palette = baseline?.Palette;
            colors = baseline?.Colors is null ? null : [.. baseline.Colors];
            colorsByKey = baseline?.ColorsByKey is null
                ? null
                : new Dictionary<string, string>(baseline.ColorsByKey);
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
            Notation = baseline?.Notation,
            Palette = palette,
            Colors = colors,
            ColorsByKey = colorsByKey,

            // Params are per chart-type; drop them on type switch so UI defaults apply
            Params = MergeParams(baseline?.Params, aiFields?.Params, chartType, chartTypeChanged),
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
