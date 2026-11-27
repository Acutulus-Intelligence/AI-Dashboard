using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Domain.Charts;

/// <summary>
/// Drops or clamps anything a client or the AI sends that the catalog does not
/// describe, so a bad style config can never reach the renderer or the database.
/// </summary>
public static partial class ChartStyleSanitizer
{
    private const int MaxColors = 64;
    private const int MaxColorKeys = 64;
    private const int MaxColorKeyLength = 120;
    private const int MaxAffixLength = 16;
    private const int MaxInfoLength = 500;
    private const int MaxDecimals = 10;

    private static readonly HashSet<string> DecimalModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "round",
        "truncate",
    };

    private static readonly HashSet<string> Notations = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "full",
        "compact",
    };

    /// <summary>
    /// Accepts hex colours and the `--chart-N` theme tokens only, which keeps
    /// arbitrary CSS (and therefore injection via `style`) out of the payload.
    /// </summary>
    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|var\(--chart-(?:1[0-6]|[1-9])\))$")]
    private static partial Regex SafeColorPattern();

    public static ChartStyleConfig? Sanitize(ChartStyleConfig? config, string chartType)
    {
        var spec = ChartCatalog.Find(chartType);
        if (spec is null || config is null) return null;

        var decimals = SanitizeDecimals(config.Decimals);
        var result = new ChartStyleConfig
        {
            Variant = spec.Variants.FirstOrDefault(v =>
                v.Id.Equals(config.Variant, StringComparison.OrdinalIgnoreCase))?.Id,
            Palette = ChartCatalog.IsKnownPalette(config.Palette) ? config.Palette!.ToLowerInvariant() : null,
            Colors = SanitizeColors(config.Colors),
            ColorsByKey = SanitizeColorsByKey(config.ColorsByKey),
            // Company-owned swatches replaced per-chart customColors.
            CustomColors = null,
            Params = SanitizeParams(config.Params, spec),
            ValuePrefix = SanitizeText(config.ValuePrefix, MaxAffixLength),
            ValueSuffix = SanitizeText(config.ValueSuffix, MaxAffixLength),
            Info = SanitizeText(config.Info, MaxInfoLength),
            Decimals = decimals,
            DecimalMode = decimals is null ? null : SanitizeDecimalMode(config.DecimalMode),
            Notation = SanitizeNotation(config.Notation),
        };

        // Theme palette and per-slice colours are mutually exclusive, but a
        // palette can act as the fallback for name-keyed overrides.
        if (!string.IsNullOrWhiteSpace(result.Palette))
        {
            result.Colors = null;
        }
        else if (result.Colors is { Count: > 0 } || result.ColorsByKey is { Count: > 0 })
        {
            result.Palette = null;
        }

        var typeSpec = ChartCatalog.Find(chartType);
        if (typeSpec is not null)
        {
            if (!typeSpec.SupportsColors)
            {
                result.Palette = null;
                result.Colors = null;
                result.ColorsByKey = null;
            }

            if (!typeSpec.SupportsValueFormat)
            {
                result.ValuePrefix = null;
                result.ValueSuffix = null;
                result.Decimals = null;
                result.DecimalMode = null;
                result.Notation = null;
            }
        }

        var isEmpty = result.Variant is null
            && result.Palette is null
            && result.Colors is null
            && result.ColorsByKey is null
            && result.Params is null
            && result.ValuePrefix is null
            && result.ValueSuffix is null
            && result.Info is null
            && result.Decimals is null
            && result.DecimalMode is null
            && result.Notation is null;

        if (isEmpty) return null;

        result.SchemaVersion = 1;
        return result;
    }

    private static string? SanitizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return null;
        // Strip control characters that could break UI / JSON round-trips.
        var cleaned = new string(trimmed.Where(c => !char.IsControl(c)).ToArray());
        if (cleaned.Length == 0) return null;
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static int? SanitizeDecimals(int? decimals)
    {
        if (decimals is null) return null;
        if (decimals < 0 || decimals > MaxDecimals) return null;
        return decimals;
    }

    private static string? SanitizeDecimalMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return "round";
        var trimmed = mode.Trim().ToLowerInvariant();
        return DecimalModes.Contains(trimmed) ? trimmed : "round";
    }

    private static string? SanitizeNotation(string? notation)
    {
        if (string.IsNullOrWhiteSpace(notation)) return null;
        var trimmed = notation.Trim().ToLowerInvariant();
        return Notations.Contains(trimmed) ? trimmed : null;
    }

    private static Dictionary<string, string>? SanitizeColorsByKey(
        Dictionary<string, string>? colors)
    {
        if (colors is null || colors.Count == 0) return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rawKey, rawColor) in colors)
        {
            if (result.Count >= MaxColorKeys) break;
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawColor)) continue;
            if (!SafeColorPattern().IsMatch(rawColor.Trim())) continue;

            var key = new string(rawKey.Trim().Where(c => !char.IsControl(c)).ToArray());
            if (key.Length == 0) continue;
            if (key.Length > MaxColorKeyLength) key = key[..MaxColorKeyLength];

            result[key] = NormalizeHex(rawColor.Trim());
        }

        return result.Count > 0 ? result : null;
    }

    private static List<string>? SanitizeColors(List<string>? colors)
    {
        if (colors is null || colors.Count == 0) return null;

        // Preserve index positions — empty slots mean "follow palette". Filtering
        // empties out would shift later overrides onto the wrong series/slices.
        var safe = colors
            .Take(MaxColors)
            .Select(c =>
            {
                if (string.IsNullOrWhiteSpace(c)) return string.Empty;
                var trimmed = c.Trim();
                return SafeColorPattern().IsMatch(trimmed) ? NormalizeHex(trimmed) : string.Empty;
            })
            .ToList();

        while (safe.Count > 0 && safe[^1].Length == 0)
            safe.RemoveAt(safe.Count - 1);

        return safe.Exists(c => c.Length > 0) ? safe : null;
    }

    private static string NormalizeHex(string color)
    {
        if (!color.StartsWith("#", StringComparison.Ordinal)) return color;
        if (color.Length is 4 or 5)
        {
            // #RGB / #RGBA → #RRGGBB (drop alpha)
            return $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
        }

        return color.Length >= 7 ? color[..7] : color;
    }

    private static Dictionary<string, JsonElement>? SanitizeParams(
        Dictionary<string, JsonElement>? values,
        ChartTypeSpec spec)
    {
        if (values is null || values.Count == 0) return null;

        var result = new Dictionary<string, JsonElement>();

        foreach (var param in spec.Params)
        {
            if (!values.TryGetValue(param.Key, out var raw)) continue;

            var sanitized = param.Kind switch
            {
                ChartParamKind.Boolean => SanitizeBoolean(raw),
                ChartParamKind.Number => SanitizeNumber(raw, param),
                ChartParamKind.Select => SanitizeSelect(raw, param),
                _ => null
            };

            if (sanitized.HasValue) result[param.Key] = sanitized.Value;
        }

        return result.Count > 0 ? result : null;
    }

    private static JsonElement? SanitizeBoolean(JsonElement raw) =>
        raw.ValueKind is JsonValueKind.True or JsonValueKind.False ? raw : null;

    private static JsonElement? SanitizeNumber(JsonElement raw, ChartParamSpec param)
    {
        if (raw.ValueKind != JsonValueKind.Number || !raw.TryGetDouble(out var value)) return null;
        if (double.IsNaN(value) || double.IsInfinity(value)) return null;

        var clamped = Math.Clamp(value, param.Min ?? double.MinValue, param.Max ?? double.MaxValue);
        return Wrap(clamped.ToString("R", CultureInfo.InvariantCulture));
    }

    private static JsonElement? SanitizeSelect(JsonElement raw, ChartParamSpec param)
    {
        if (raw.ValueKind != JsonValueKind.String) return null;

        var value = raw.GetString();
        var match = param.Options?.FirstOrDefault(o =>
            o.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : Wrap(JsonSerializer.Serialize(match.Value));
    }

    private static JsonElement Wrap(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
