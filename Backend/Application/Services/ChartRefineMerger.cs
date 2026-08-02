using Application.DTos.Request;
using Domain.Charts;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// After a refine call, prefer the AI result for data fields and AI-controlled style
/// fields (variant, info, decimals, decimalMode, prefix/suffix). Always preserve
/// baseline colours, palette, and params — those are UI-controlled.
/// </summary>
public static class ChartRefineMerger
{
    public static AiChartConfig Apply(ChartBaseline baseline, AiChartConfig ai, string userPrompt)
    {
        // userPrompt reserved for future intent hints
        _ = userPrompt;

        var chartType = !string.IsNullOrWhiteSpace(ai.ChartType) ? ai.ChartType : baseline.ChartType;
        var chartTypeChanged = !string.Equals(chartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);

        var mergedStyle = MergeStyle(baseline.StyleConfig, ai.StyleConfig, chartType, chartTypeChanged);

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
    /// Style fields the AI is allowed to set. Colours / palette / params are stripped.
    /// </summary>
    public static ChartStyleConfig? TakeAiControlledStyleFields(ChartStyleConfig? ai, string chartType)
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
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

        // Drop empty objects so sanitize/defaults apply cleanly.
        if (result.Variant is null
            && result.ValuePrefix is null
            && result.ValueSuffix is null
            && result.Info is null
            && result.Decimals is null
            && result.DecimalMode is null)
            return null;

        return result;
    }

    /// <summary>
    /// Baseline style sent to the model — no colours/params noise in the prompt.
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
        };

        if (slim.Variant is null
            && slim.ValuePrefix is null
            && slim.ValueSuffix is null
            && slim.Info is null
            && slim.Decimals is null
            && slim.DecimalMode is null)
            return null;

        return slim;
    }

    private static ChartStyleConfig? MergeStyle(
        ChartStyleConfig? baseline,
        ChartStyleConfig? ai,
        string chartType,
        bool chartTypeChanged)
    {
        if (ai is null && baseline is null) return null;

        var aiFields = TakeAiControlledStyleFields(ai, chartType);

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

            // Always UI / baseline — never from AI
            Palette = baseline?.Palette,
            Colors = baseline?.Colors is null ? null : [.. baseline.Colors],
            CustomColors = baseline?.CustomColors is null ? null : [.. baseline.CustomColors],
            // Params are per chart-type; drop them on type switch so UI defaults apply
            Params = chartTypeChanged ? null : baseline?.Params,
        };

        if (result.DecimalMode is not null && result.Decimals is null)
            result.Decimals = 0;

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

    private static string? NormalizeDecimalMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        var trimmed = mode.Trim().ToLowerInvariant();
        return trimmed is "round" or "truncate" ? trimmed : null;
    }
}
