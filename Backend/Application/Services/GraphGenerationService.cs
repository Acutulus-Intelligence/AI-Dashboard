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

        // Slim baseline for the prompt — no colours/params noise.
        var currentChartJson = request.CurrentChart is null
            ? null
            : JsonSerializer.Serialize(new
            {
                request.CurrentChart.Title,
                request.CurrentChart.ChartType,
                request.CurrentChart.XAxis,
                request.CurrentChart.YAxis,
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
            ct);

        var config = aiResult.Config;
        var notes = new List<string>();

        if (request.CurrentChart is not null)
        {
            if (string.IsNullOrWhiteSpace(aiResult.Config.ChartType)
                || string.IsNullOrWhiteSpace(aiResult.Config.SqlQuery))
            {
                notes.Add("AI omitted chartType and/or sqlQuery; filled from baseline.");
            }

            // Preserve baseline colours/params; take AI variant/info/decimals/prefix/suffix only.
            config = ChartRefineMerger.Apply(request.CurrentChart, config, prompt);
            config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
            notes.Add("Merged AI style fields onto baseline; colours/params kept from baseline.");
        }
        else
        {
            // First generate: ignore any AI colours/params so UI defaults apply.
            config.StyleConfig = ChartStyleSanitizer.Sanitize(
                ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType),
                config.ChartType);
            notes.Add("First generate: stripped AI colours/params.");
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

        var aiResult = await _aiService.GenerateChartConfigAsync(schemaJson, prompt, dbProvider, request.PrefabChartType, ct: ct);
        var config = aiResult.Config;
        var notes = new List<string> { "Manual generate path." };

        config.StyleConfig = ChartStyleSanitizer.Sanitize(
            ChartRefineMerger.TakeAiControlledStyleFields(config.StyleConfig, config.ChartType),
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
