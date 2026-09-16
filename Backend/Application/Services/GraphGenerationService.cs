using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Services;

public class GraphGenerationService : IGraphGenerationService
{
    private readonly IApplicationDbContext _db;
    private readonly ISchemaInspector _schemaInspector;
    private readonly IAiService _aiService;
    private readonly ISqlValidator _sqlValidator;
    private readonly IQueryExecutor _queryExecutor;
    private readonly IConnectionAccessService _access;

    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public GraphGenerationService(
        IApplicationDbContext db,
        ISchemaInspector schemaInspector,
        IAiService aiService,
        ISqlValidator sqlValidator,
        IQueryExecutor queryExecutor,
        IConnectionAccessService access)
    {
        _db = db;
        _schemaInspector = schemaInspector;
        _aiService = aiService;
        _sqlValidator = sqlValidator;
        _queryExecutor = queryExecutor;
        _access = access;
    }

    public async Task<ChartConfigResponse> GenerateAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default)
    {
        var dbProvider = await GetDbProviderAsync(request.ConnectionId, userId, ct);
        var schema = await _schemaInspector.GetTableSchemaAsync(request.ConnectionId, userId, request.TableName, ct);
        var allowedColors = await ResolveAccountColorsAsync(userId, ct);

        var schemaJson = ChartGenerationSchema.ToJson(schema, allowedColors);

        var prompt = request.Mode switch
        {
            "prompt" => request.Prompt ?? "Show me this data in a chart.",
            "prefab" => $"Create a {request.PrefabChartType} chart for this table.",
            "auto" => "Choose the best visualization for this data.",
            _ => "Show me this data in a chart."
        };

        // Slim baseline for the prompt — colours included; params omitted.
        // SeriesColourSlots documents yAxis index → name for named colours.
        var currentChartJson = request.CurrentChart is null
            ? null
            : JsonSerializer.Serialize(new
            {
                request.CurrentChart.Title,
                request.CurrentChart.ChartType,
                request.CurrentChart.XAxis,
                request.CurrentChart.YAxis,
                SeriesColourSlots = request.CurrentChart.YAxis
                    .Select((name, i) => $"{i}={name}")
                    .ToList(),
                request.CurrentChart.Aggregation,
                request.CurrentChart.GroupBy,
                request.CurrentChart.SqlQuery,
                StyleConfig = ChartRefineMerger.SlimStyleForAi(request.CurrentChart.StyleConfig),
            }, BaselineJsonOptions);

        var (aiResult, retryNotes) = await AiGenerationRetry.RunAsync(
            (repairHint, token) => _aiService.GenerateChartConfigAsync(
                schemaJson,
                prompt,
                dbProvider,
                request.PrefabChartType,
                currentChartJson,
                allowedColors,
                repairHint,
                token),
            result =>
            {
                var generated = FinalizeGeneratedConfig(result.Config, request.CurrentChart, prompt, allowedColors, notes: null);
                if (string.IsNullOrWhiteSpace(generated.ChartType) || string.IsNullOrWhiteSpace(generated.SqlQuery))
                {
                    return "Chart config is missing required fields (chartType, sqlQuery).";
                }

                if (TryAcceptSql(generated, schema, request.CurrentChart, notes: null, out var retryHint, out _))
                    return null;

                return retryHint ?? "SQL failed schema grounding.";
            },
            ct);

        var notes = new List<string>(retryNotes);
        var config = FinalizeGeneratedConfig(aiResult.Config, request.CurrentChart, prompt, allowedColors, notes);

        if (string.IsNullOrWhiteSpace(config.ChartType) || string.IsNullOrWhiteSpace(config.SqlQuery))
        {
            throw new InvalidOperationException(
                "Chart config is missing required fields (chartType, sqlQuery) after merge. " +
                $"Raw: {Truncate(aiResult.RawJson, 400)}");
        }

        if (!TryAcceptSql(config, schema, request.CurrentChart, notes, out var sqlError, out var accepted))
        {
            throw new InvalidOperationException(
                $"AI generated an invalid query: {sqlError}. " +
                $"chartType={config.ChartType}. SQL: {Truncate(config.SqlQuery, 400)}");
        }

        config = accepted;

        var result = await ExecuteWithSchemaRepairAsync(
            request.ConnectionId,
            userId,
            schemaJson,
            prompt,
            dbProvider,
            request.PrefabChartType,
            currentChartJson,
            allowedColors,
            schema,
            request.CurrentChart,
            config,
            aiResult,
            notes,
            ct);

        return new ChartConfigResponse(
            result.Config.ChartType,
            result.Config.Title,
            result.Config.XAxis,
            result.Config.YAxis,
            result.Config.Aggregation,
            result.Config.GroupBy,
            result.Config.SqlQuery,
            result.Rows,
            result.Config.StyleConfig,
            AiDebug: BuildDebug(result.AiResult, result.Config, notes)
        );
    }

    public async Task<ChartConfigResponse> ManualAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default)
    {
        var dbProvider = await GetDbProviderAsync(request.ConnectionId, userId, ct);
        var schema = await _schemaInspector.GetTableSchemaAsync(request.ConnectionId, userId, request.TableName, ct);
        var allowedColors = await ResolveAccountColorsAsync(userId, ct);

        var schemaJson = ChartGenerationSchema.ToJson(schema, allowedColors);

        var prompt = $"Create a {request.PrefabChartType ?? "bar"} chart for this table. Use xAxis={request.Prompt ?? ""} for x-axis.";

        var (aiResult, retryNotes) = await AiGenerationRetry.RunAsync(
            (repairHint, token) => _aiService.GenerateChartConfigAsync(
                schemaJson, prompt, dbProvider, request.PrefabChartType,
                allowedColors: allowedColors, repairHint: repairHint, ct: token),
            result =>
            {
                var generated = FinalizeGeneratedConfig(result.Config, baseline: null, prompt, allowedColors, notes: null);
                if (string.IsNullOrWhiteSpace(generated.ChartType) || string.IsNullOrWhiteSpace(generated.SqlQuery))
                    return "Chart config is missing required fields (chartType, sqlQuery).";

                return TryAcceptSql(generated, schema, baseline: null, notes: null, out var retryHint, out _)
                    ? null
                    : retryHint ?? "SQL failed schema grounding.";
            },
            ct);

        var notes = new List<string>(retryNotes) { "Manual generate path." };
        var config = FinalizeGeneratedConfig(aiResult.Config, baseline: null, prompt, allowedColors, notes);

        if (!TryAcceptSql(config, schema, baseline: null, notes, out var sqlError, out var accepted))
        {
            throw new InvalidOperationException(
                $"AI generated an invalid query: {sqlError}. " +
                $"chartType={config.ChartType}. SQL: {Truncate(config.SqlQuery, 400)}");
        }

        config = accepted;

        var result = await ExecuteWithSchemaRepairAsync(
            request.ConnectionId,
            userId,
            schemaJson,
            prompt,
            dbProvider,
            request.PrefabChartType,
            currentChartJson: null,
            allowedColors,
            schema,
            baseline: null,
            config,
            aiResult,
            notes,
            ct);

        return new ChartConfigResponse(
            result.Config.ChartType,
            result.Config.Title,
            result.Config.XAxis,
            result.Config.YAxis,
            result.Config.Aggregation,
            result.Config.GroupBy,
            result.Config.SqlQuery,
            result.Rows,
            result.Config.StyleConfig,
            AiDebug: BuildDebug(result.AiResult, result.Config, notes)
        );
    }

    private static AiChartConfig FinalizeGeneratedConfig(
        AiChartConfig config,
        ChartBaseline? baseline,
        string prompt,
        IReadOnlyList<string>? allowedColors,
        List<string>? notes)
    {
        var seriesKeys = baseline?.YAxis is { Count: > 0 } baselineY
            ? (IReadOnlyList<string>)baselineY
            : config.YAxis;
        if (config.NamedColorMap is { Count: > 0 })
        {
            config.StyleConfig ??= new ChartStyleConfig();
            config.StyleConfig.Colors = ChartRefineMerger.ExpandNamedColorMap(config.NamedColorMap, seriesKeys);
            config.StyleConfig.Palette = null;
            notes?.Add("Expanded named styleConfig.colors onto yAxis/series order.");
        }

        var styleHint = GeneratedChartGrounding.ValidateStyle(config, allowedColors);
        if (styleHint is not null)
            notes?.Add($"Style clamped after AI output: {styleHint}");

        if (baseline is not null)
        {
            if (string.IsNullOrWhiteSpace(config.ChartType) || string.IsNullOrWhiteSpace(config.SqlQuery))
                notes?.Add("AI omitted chartType and/or sqlQuery; filled from baseline.");

            config = ChartRefineMerger.Apply(baseline, config, prompt, allowedColors);
            config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
            notes?.Add(
                ChartRefineMerger.RequestsStyleChange(prompt)
                    ? "Merged AI style fields (user requested a style change); params kept from baseline."
                    : "Preserved baseline style — prompt had no explicit style/colour request.");
            return config;
        }

        config.StyleConfig = ChartStyleSanitizer.Sanitize(
            ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType, allowedColors),
            config.ChartType);
        notes?.Add("First generate: colours clamped to account palette; params stripped.");
        return config;
    }

    /// <summary>
    /// Accepts SQL that is SELECT-only and grounded in the connected table schema.
    /// On refine with an unchanged chart type, falls back to baseline SQL when the
    /// model invented identifiers or mangled the query during a style-only edit.
    /// </summary>
    private bool TryAcceptSql(
        AiChartConfig config,
        TableSchema schema,
        ChartBaseline? baseline,
        List<string>? notes,
        out string? retryHint,
        out AiChartConfig accepted)
    {
        accepted = config;
        retryHint = GeneratedChartGrounding.ValidateSqlChart(config, schema, _sqlValidator);
        if (retryHint is null)
            return true;

        var typeChanged = baseline is not null
            && !string.Equals(config.ChartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);

        if (baseline is not null && !typeChanged)
        {
            var baselineConfig = new AiChartConfig
            {
                ChartType = baseline.ChartType,
                Title = baseline.Title,
                XAxis = baseline.XAxis,
                YAxis = [.. baseline.YAxis],
                Aggregation = baseline.Aggregation,
                GroupBy = baseline.GroupBy,
                SqlQuery = baseline.SqlQuery,
            };

            if (GeneratedChartGrounding.ValidateSqlChart(baselineConfig, schema, _sqlValidator) is null)
            {
                notes?.Add(
                    $"AI SQL failed schema grounding ({retryHint}); kept baseline query fields. Rejected SQL: {Truncate(config.SqlQuery, 240)}");
                config.SqlQuery = baseline.SqlQuery;
                config.XAxis = baseline.XAxis;
                config.YAxis = [.. baseline.YAxis];
                config.Aggregation = baseline.Aggregation;
                config.GroupBy = baseline.GroupBy;
                accepted = config;
                retryHint = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Company palette when the user belongs to a company; otherwise theme defaults.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveAccountColorsAsync(Guid userId, CancellationToken ct)
    {
        var companyStyle = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Company != null ? u.Company.StyleConfig : null)
            .FirstOrDefaultAsync(ct);

        return companyStyle is null
            ? CompanyStyleSanitizer.DefaultColors
            : CompanyStyleSanitizer.ResolveColors(companyStyle);
    }

    private static AiGenerationDebug BuildDebug(
        AiChartResult aiResult,
        AiChartConfig finalConfig,
        List<string> notes) =>
        new(
            Truncate(aiResult.RawJson, 6000),
            finalConfig.ChartType,
            finalConfig.SqlQuery,
            finalConfig.StyleConfig,
            aiResult.FinishReason,
            notes);

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        return value.Length <= max ? value : value[..max] + "…";
    }

    private async Task<DbProvider> GetDbProviderAsync(Guid connectionId, Guid userId, CancellationToken ct)
    {
        var connection = await _access.FindViewableAsync(connectionId, userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        return connection.DbProvider;
    }

    private async Task<(AiChartConfig Config, AiChartResult AiResult, List<Dictionary<string, object?>> Rows)> ExecuteWithSchemaRepairAsync(
        Guid connectionId,
        Guid userId,
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType,
        string? currentChartJson,
        IReadOnlyList<string>? allowedColors,
        TableSchema schema,
        ChartBaseline? baseline,
        AiChartConfig config,
        AiChartResult aiResult,
        List<string> notes,
        CancellationToken ct)
    {
        try
        {
            var rows = await _queryExecutor.ExecuteAsync(connectionId, userId, config.SqlQuery, ct);
            return (config, aiResult, rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   && AiGenerationRetry.IsLikelySchemaError(ex.Message))
        {
            notes.Add($"Query execution failed with a schema error; requesting one repair. {ex.Message}");
            var repaired = await _aiService.GenerateChartConfigAsync(
                schemaJson,
                prompt,
                dbProvider,
                prefabChartType,
                currentChartJson,
                allowedColors,
                repairHint: ex.Message,
                ct);

            config = FinalizeGeneratedConfig(repaired.Config, baseline, prompt, allowedColors, notes);
            if (!TryAcceptSql(config, schema, baseline, notes, out var sqlError, out var accepted))
            {
                throw new InvalidOperationException(
                    $"AI generated an invalid query after execution repair: {sqlError}. " +
                    $"Original execution error: {ex.Message}. SQL: {Truncate(config.SqlQuery, 400)}");
            }

            var rows = await _queryExecutor.ExecuteAsync(connectionId, userId, accepted.SqlQuery, ct);
            return (accepted, repaired, rows);
        }
    }
}
