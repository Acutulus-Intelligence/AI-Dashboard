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

        var schemaJson = AiSchemaContext.ToPromptJson(schema);

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

        var notes = new List<string>();
        var userPrompt = prompt;
        AiChartResult? aiResult = null;
        AiChartConfig? config = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var attemptNotes = new List<string>();
            aiResult = await _aiService.GenerateChartConfigAsync(
                schemaJson,
                userPrompt,
                dbProvider,
                request.PrefabChartType,
                currentChartJson,
                allowedColors,
                ct);

            config = FinalizeGeneratedConfig(
                aiResult.Config,
                request.CurrentChart,
                prompt,
                allowedColors,
                attemptNotes,
                aiResult.RawJson ?? string.Empty);

            var issues = CollectSqlIssues(config, schema);
            if (issues.Count == 0)
            {
                notes.AddRange(attemptNotes);
                break;
            }

            if (attempt < 2)
            {
                notes.Add(
                    $"Attempt {attempt} rejected invented or invalid identifiers: {string.Join("; ", issues)}. Retrying with schema repair instructions.");
                userPrompt = prompt + AiOutputGrounding.BuildRepairSuffix(
                    issues,
                    AiSchemaContext.TableNames(schema),
                    AiSchemaContext.ColumnNames(schema));
                continue;
            }

            notes.AddRange(attemptNotes);
            if (TryFallbackToBaseline(config, request.CurrentChart, issues, notes))
                break;

            throw new InvalidOperationException(
                "AI generated a query that uses tables or columns that are not in the schema: "
                + string.Join(" ", issues));
        }

        if (config is null || aiResult is null)
            throw new InvalidOperationException("AI did not return a chart configuration.");

        var result = await _queryExecutor.ExecuteAsync(request.ConnectionId, userId, config.SqlQuery, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            result,
            config.StyleConfig,
            AiDebug: BuildDebug(aiResult, config, notes)
        );
    }

    public async Task<ChartConfigResponse> ManualAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default)
    {
        var dbProvider = await GetDbProviderAsync(request.ConnectionId, userId, ct);
        var schema = await _schemaInspector.GetTableSchemaAsync(request.ConnectionId, userId, request.TableName, ct);
        var allowedColors = await ResolveAccountColorsAsync(userId, ct);

        var schemaJson = AiSchemaContext.ToPromptJson(schema);

        var prompt = $"Create a {request.PrefabChartType ?? "bar"} chart for this table. Use xAxis={request.Prompt ?? ""} for x-axis.";
        var notes = new List<string> { "Manual generate path." };
        var userPrompt = prompt;
        AiChartResult? aiResult = null;
        AiChartConfig? config = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var attemptNotes = new List<string>();
            aiResult = await _aiService.GenerateChartConfigAsync(
                schemaJson, userPrompt, dbProvider, request.PrefabChartType, allowedColors: allowedColors, ct: ct);
            config = FinalizeGeneratedConfig(
                aiResult.Config, baseline: null, prompt, allowedColors, attemptNotes, aiResult.RawJson ?? string.Empty);

            var issues = CollectSqlIssues(config, schema);
            if (issues.Count == 0)
            {
                notes.AddRange(attemptNotes);
                break;
            }

            if (attempt < 2)
            {
                notes.Add(
                    $"Attempt {attempt} rejected invented or invalid identifiers: {string.Join("; ", issues)}. Retrying with schema repair instructions.");
                userPrompt = prompt + AiOutputGrounding.BuildRepairSuffix(
                    issues,
                    AiSchemaContext.TableNames(schema),
                    AiSchemaContext.ColumnNames(schema));
                continue;
            }

            notes.AddRange(attemptNotes);
            throw new InvalidOperationException(
                "AI generated a query that uses tables or columns that are not in the schema: "
                + string.Join(" ", issues));
        }

        if (config is null || aiResult is null)
            throw new InvalidOperationException("AI did not return a chart configuration.");

        var result = await _queryExecutor.ExecuteAsync(request.ConnectionId, userId, config.SqlQuery, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            result,
            config.StyleConfig,
            AiDebug: BuildDebug(aiResult, config, notes)
        );
    }

    private AiChartConfig FinalizeGeneratedConfig(
        AiChartConfig config,
        ChartBaseline? baseline,
        string prompt,
        IReadOnlyList<string>? allowedColors,
        List<string> notes,
        string rawJson)
    {
        var seriesKeys = baseline?.YAxis is { Count: > 0 } baselineY
            ? (IReadOnlyList<string>)baselineY
            : config.YAxis;
        if (config.NamedColorMap is { Count: > 0 })
        {
            config.StyleConfig ??= new ChartStyleConfig();
            config.StyleConfig.Colors = ChartRefineMerger.ExpandNamedColorMap(config.NamedColorMap, seriesKeys);
            config.StyleConfig.Palette = null;
            notes.Add("Expanded named styleConfig.colors onto yAxis/series order.");
        }

        if (baseline is not null)
        {
            if (string.IsNullOrWhiteSpace(config.ChartType) || string.IsNullOrWhiteSpace(config.SqlQuery))
                notes.Add("AI omitted chartType and/or sqlQuery; filled from baseline.");

            config = ChartRefineMerger.Apply(baseline, config, prompt, allowedColors);
            config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
            notes.Add(
                ChartRefineMerger.RequestsStyleChange(prompt)
                    ? "Merged AI style fields (user requested a style change); params kept from baseline."
                    : "Preserved baseline style — prompt had no explicit style/colour request.");
        }
        else
        {
            config.StyleConfig = ChartStyleSanitizer.Sanitize(
                ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType, allowedColors),
                config.ChartType);
            notes.Add("First generate: colours clamped to account palette; params stripped.");
        }

        if (string.IsNullOrWhiteSpace(config.ChartType) || string.IsNullOrWhiteSpace(config.SqlQuery))
        {
            throw new InvalidOperationException(
                "Chart config is missing required fields (chartType, sqlQuery) after merge. " +
                $"Raw: {Truncate(rawJson, 400)}");
        }

        return config;
    }

    private List<string> CollectSqlIssues(AiChartConfig config, TableSchema schema)
    {
        var issues = new List<string>();
        if (!_sqlValidator.IsSelectOnly(config.SqlQuery, out var errorMessage))
            issues.Add(errorMessage ?? "SQL is not a safe SELECT.");
        issues.AddRange(AiOutputGrounding.ValidateSqlChart(config, schema).Errors);
        return issues;
    }

    /// <summary>
    /// On refine with unchanged chart type, keep baseline SQL/axes when the model
    /// invented identifiers or mangled the query.
    /// </summary>
    private static bool TryFallbackToBaseline(
        AiChartConfig config,
        ChartBaseline? baseline,
        IReadOnlyList<string> issues,
        List<string> notes)
    {
        var typeChanged = baseline is not null
            && !string.Equals(config.ChartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);

        if (baseline is null || typeChanged)
            return false;

        notes.Add(
            $"AI output failed grounding ({string.Join("; ", issues)}); kept baseline SQL and axes. Rejected SQL: {Truncate(config.SqlQuery, 240)}");
        config.SqlQuery = baseline.SqlQuery;
        config.XAxis = baseline.XAxis;
        config.YAxis = [.. baseline.YAxis];
        config.Aggregation = baseline.Aggregation;
        config.GroupBy = baseline.GroupBy;
        return true;
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
}
