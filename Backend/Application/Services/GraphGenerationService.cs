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

        var schemaJson = JsonSerializer.Serialize(new
        {
            table = schema.TableName,
            columns = schema.Columns.Select(c => new
            {
                name = c.ColumnName,
                type = c.DataType,
                nullable = c.IsNullable
            })
        });

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

        var aiResult = await _aiService.GenerateChartConfigAsync(
            schemaJson,
            prompt,
            dbProvider,
            request.PrefabChartType,
            currentChartJson,
            allowedColors,
            ct);

        var config = aiResult.Config;
        var notes = new List<string>();

        // Named colour objects need yAxis order (prefer baseline on refine).
        var seriesKeys = request.CurrentChart?.YAxis is { Count: > 0 } baselineY
            ? (IReadOnlyList<string>)baselineY
            : config.YAxis;
        if (config.NamedColorMap is { Count: > 0 })
        {
            config.StyleConfig ??= new ChartStyleConfig();
            config.StyleConfig.Colors = ChartRefineMerger.ExpandNamedColorMap(config.NamedColorMap, seriesKeys);
            config.StyleConfig.Palette = null;
            notes.Add("Expanded named styleConfig.colors onto yAxis/series order.");
        }

        if (request.CurrentChart is not null)
        {
            if (string.IsNullOrWhiteSpace(aiResult.Config.ChartType)
                || string.IsNullOrWhiteSpace(aiResult.Config.SqlQuery))
            {
                notes.Add("AI omitted chartType and/or sqlQuery; filled from baseline.");
            }

            // Take AI style only when the user asked for a style change; else keep baseline.
            config = ChartRefineMerger.Apply(request.CurrentChart, config, prompt, allowedColors);
            config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
            notes.Add(
                ChartRefineMerger.RequestsStyleChange(prompt)
                    ? "Merged AI style fields (user requested a style change); params kept from baseline."
                    : "Preserved baseline style — prompt had no explicit style/colour request.");
        }
        else
        {
            // First generate: allow AI colours from account palette; strip params.
            config.StyleConfig = ChartStyleSanitizer.Sanitize(
                ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType, allowedColors),
                config.ChartType);
            notes.Add("First generate: colours clamped to account palette; params stripped.");
        }

        if (string.IsNullOrWhiteSpace(config.ChartType) || string.IsNullOrWhiteSpace(config.SqlQuery))
        {
            throw new InvalidOperationException(
                "Chart config is missing required fields (chartType, sqlQuery) after merge. " +
                $"Raw: {Truncate(aiResult.RawJson, 400)}");
        }

        config = EnsureValidSql(config, request.CurrentChart, notes);

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

        var schemaJson = JsonSerializer.Serialize(new
        {
            table = schema.TableName,
            columns = schema.Columns.Select(c => new
            {
                name = c.ColumnName,
                type = c.DataType,
                nullable = c.IsNullable
            })
        });

        var prompt = $"Create a {request.PrefabChartType ?? "bar"} chart for this table. Use xAxis={request.Prompt ?? ""} for x-axis.";

        var aiResult = await _aiService.GenerateChartConfigAsync(
            schemaJson, prompt, dbProvider, request.PrefabChartType, allowedColors: allowedColors, ct: ct);
        var config = aiResult.Config;
        var notes = new List<string> { "Manual generate path." };

        if (config.NamedColorMap is { Count: > 0 })
        {
            config.StyleConfig ??= new ChartStyleConfig();
            config.StyleConfig.Colors = ChartRefineMerger.ExpandNamedColorMap(config.NamedColorMap, config.YAxis);
            config.StyleConfig.Palette = null;
            notes.Add("Expanded named styleConfig.colors onto yAxis order.");
        }

        config.StyleConfig = ChartStyleSanitizer.Sanitize(
            ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType, allowedColors),
            config.ChartType);

        config = EnsureValidSql(config, baseline: null, notes);

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

    /// <summary>
    /// Rejects invalid SQL. On refine with unchanged chart type, falls back to baseline SQL
    /// when the model mangled the query during a style-only edit.
    /// </summary>
    private AiChartConfig EnsureValidSql(
        AiChartConfig config,
        ChartBaseline? baseline,
        List<string> notes)
    {
        if (_sqlValidator.IsSelectOnly(config.SqlQuery, out var errorMessage))
            return config;

        var typeChanged = baseline is not null
            && !string.Equals(config.ChartType, baseline.ChartType, StringComparison.OrdinalIgnoreCase);

        if (baseline is not null
            && !typeChanged
            && _sqlValidator.IsSelectOnly(baseline.SqlQuery, out _))
        {
            notes.Add(
                $"AI SQL failed validation ({errorMessage}); kept baseline SQL. Rejected SQL: {Truncate(config.SqlQuery, 240)}");
            config.SqlQuery = baseline.SqlQuery;
            return config;
        }

        throw new InvalidOperationException(
            $"AI generated an invalid query: {errorMessage}. " +
            $"chartType={config.ChartType}. SQL: {Truncate(config.SqlQuery, 400)}");
    }

    /// <summary>
    /// Company palette when the user belongs to a company; otherwise theme defaults.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveAccountColorsAsync(Guid userId, CancellationToken ct)
    {
        var companyId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is null)
            return CompanyStyleSanitizer.DefaultColors;

        var style = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.StyleConfig)
            .FirstOrDefaultAsync(ct);

        return CompanyStyleSanitizer.ResolveColors(style);
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
