using System.Text.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;

namespace Application.Services;

public class GraphGenerationService : IGraphGenerationService
{
    private readonly ISchemaInspector _schemaInspector;
    private readonly IAiService _aiService;
    private readonly ISqlValidator _sqlValidator;
    private readonly IQueryExecutor _queryExecutor;

    public GraphGenerationService(
        ISchemaInspector schemaInspector,
        IAiService aiService,
        ISqlValidator sqlValidator,
        IQueryExecutor queryExecutor)
    {
        _schemaInspector = schemaInspector;
        _aiService = aiService;
        _sqlValidator = sqlValidator;
        _queryExecutor = queryExecutor;
    }

    public async Task<ChartConfigResponse> GenerateAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default)
    {
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

        var config = await _aiService.GenerateChartConfigAsync(schemaJson, prompt, request.PrefabChartType, ct);

        if (!_sqlValidator.IsSelectOnly(config.SqlQuery, out var errorMessage))
            throw new InvalidOperationException($"AI generated an invalid query: {errorMessage}");

        var result = await _queryExecutor.ExecuteAsync(request.ConnectionId, userId, config.SqlQuery, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            result
        );
    }

    public async Task<ChartConfigResponse> ManualAsync(GenerateChartRequest request, Guid userId, CancellationToken ct = default)
    {
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

        var config = await _aiService.GenerateChartConfigAsync(schemaJson, prompt, request.PrefabChartType, ct);

        if (!_sqlValidator.IsSelectOnly(config.SqlQuery, out var errorMessage))
            throw new InvalidOperationException($"AI generated an invalid query: {errorMessage}");

        var result = await _queryExecutor.ExecuteAsync(request.ConnectionId, userId, config.SqlQuery, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            result
        );
    }
}
