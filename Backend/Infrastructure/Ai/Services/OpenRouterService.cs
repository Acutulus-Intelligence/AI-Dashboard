using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
using Infrastructure.ExternalDb.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai.Services;

public class OpenRouterService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(
        HttpClient httpClient,
        IOptions<AiSettings> settings,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiChartConfig> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(schemaJson, dbProvider, prefabChartType);
        var config = await SendChatAsync(systemPrompt, prompt, ct);

        if (string.IsNullOrEmpty(config.ChartType) || string.IsNullOrEmpty(config.SqlQuery))
            throw new InvalidOperationException("AI response is missing required fields (chartType, sqlQuery).");

        return FinalizeConfig(config);
    }

    public async Task<AiChartConfig> GenerateCollectionChartConfigAsync(
        string schemaJson,
        string prompt,
        string? prefabChartType = null,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildCollectionSystemPrompt(schemaJson, prefabChartType);
        var config = await SendChatAsync(systemPrompt, prompt, ct);

        if (string.IsNullOrEmpty(config.ChartType) || config.DataModel is null)
            throw new InvalidOperationException("AI response is missing required fields (chartType, dataModel).");

        config.SqlQuery = string.Empty;
        return FinalizeConfig(config);
    }

    private async Task<AiChartConfig> SendChatAsync(string systemPrompt, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_object" },
            max_tokens = _settings.MaxTokens,
            temperature = _settings.Temperature
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/Acutulus-Intelligence/AI-Dashboard");
        request.Headers.Add("X-OpenRouter-Title", "AI-Dashboard");

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(cancellationToken: ct);

        var content = responseBody?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("AI returned an empty response.");

        return JsonSerializer.Deserialize<AiChartConfig>(content)
            ?? throw new InvalidOperationException("Failed to parse AI response as chart config.");
    }

    private AiChartConfig FinalizeConfig(AiChartConfig config)
    {
        if (!ChartCatalog.IsKnownType(config.ChartType))
            throw new InvalidOperationException(
                $"AI returned unsupported chart type '{config.ChartType}'. " +
                $"Supported types: {string.Join(", ", ChartCatalog.TypeIds)}.");

        // Models routinely invent variants and parameters, so keep only what the
        // catalog actually describes rather than trusting the response.
        config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);

        _logger.LogInformation(
            "AI chart config generated: chartType={ChartType}, sqlLength={SqlLength}",
            config.ChartType,
            config.SqlQuery.Length);
        _logger.LogDebug("AI chart config response received with title={Title}", config.Title);

        return config;
    }

    private static string BuildSystemPrompt(string schemaJson, DbProvider dbProvider, string? prefabChartType)
    {
        var chartPreference = prefabChartType switch
        {
            not null => $"The user prefers the chart type: {prefabChartType}.",
            null => "Choose the best chart type based on the data."
        };

        var quotingRule = SqlIdentifierQuoter.GetQuotingRule(dbProvider);
        var dbName = dbProvider switch
        {
            DbProvider.PostgreSql => "PostgreSQL",
            DbProvider.MySql => "MySQL",
            DbProvider.SqlServer => "SQL Server",
            DbProvider.Sqlite => "SQLite",
            _ => "SQL"
        };

        var template = @"
You are a data visualization assistant. Given a database table schema, generate a chart configuration.
Return ONLY valid JSON — no markdown, no code fences, no extra text.

Database: __DBNAME__
Table schema:
__SCHEMA__

__PREFERENCE__

Available chart types, their variants and their adjustable parameters:
__CATALOG__

Available palettes: __PALETTES__

Return this exact JSON structure:
{
  ""chartType"": __TYPE_UNION__,
  ""title"": ""string — concise chart title"",
  ""xAxis"": ""column_name — the column for the x-axis / labels"",
  ""yAxis"": [""column_name — one or more columns for the y-axis / values""],
  ""aggregation"": ""sum"" | ""avg"" | ""count"" | ""min"" | ""max"" | ""none"",
  ""groupBy"": ""column_name | null — column to group by, or null"",
  ""sqlQuery"": ""SELECT ... — a safe, valid SELECT query that fetches the data needed"",
  ""styleConfig"": {
    ""variant"": ""one of the variant ids listed for the chosen chartType"",
    ""palette"": ""one of the palette ids listed above"",
    ""params"": { ""paramKey"": value }
  }
}

Rules:
- sqlQuery must be a valid SELECT query only for __DBNAME__
- __QUOTING_RULE__
- Never include actual data values — only column names and SQL
- styleConfig.variant must be a variant of the chartType you chose
- styleConfig.params may only use the parameter keys listed for that chartType, and must respect the stated types and ranges
- Omit styleConfig fields you have no opinion about rather than guessing
- The JSON must be parseable and complete
";

        return template
            .Replace("__DBNAME__", dbName)
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__PALETTES__", string.Join(", ", ChartCatalog.Palettes.Select(p => p.Id)))
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")))
            .Replace("__QUOTING_RULE__", quotingRule);
    }

    private static string BuildCollectionSystemPrompt(string schemaJson, string? prefabChartType)
    {
        var chartPreference = prefabChartType switch
        {
            not null => $"The user prefers the chart type: {prefabChartType}.",
            null => "Choose the best chart type based on the data."
        };

        var template = @"
You are a data visualization assistant. Given the schema of uploaded tabular data, generate a chart configuration.
Return ONLY valid JSON — no markdown, no code fences, no extra text.

Data schema (columns and their inferred types):
__SCHEMA__

__PREFERENCE__

Available chart types, their variants and their adjustable parameters:
__CATALOG__

Available palettes: __PALETTES__

This data lives in memory, not in a database. Do NOT generate SQL — instead build a structured query (dataModel) that is applied to the rows in memory.

Return this exact JSON structure:
{
  ""chartType"": __TYPE_UNION__,
  ""title"": ""string — concise chart title"",
  ""xAxis"": ""column_name — the column used for labels / categories"",
  ""yAxis"": [""column_name — columns used as values; must exist in the dataModel output""],
  ""aggregation"": ""sum"" | ""avg"" | ""count"" | ""min"" | ""max"" | ""none"",
  ""groupBy"": ""column_name | null — column to group by, or null"",
  ""dataModel"": {
    ""filters"": [
      { ""column"": ""column_name"", ""operator"": ""eq""|""neq""|""gt""|""gte""|""lt""|""lte""|""contains""|""in""|""notin""|""isnull""|""isnotnull"", ""value"": ""string — raw filter value, e.g. a category name, number, or comma-separated list for in/notin"" }
    ],
    ""groupBy"": [""column_name — one or more group columns""],
    ""aggregations"": [
      { ""column"": ""column_name"", ""function"": ""count""|""sum""|""avg""|""min""|""max"" }
    ],
    ""orderBy"": [
      { ""column"": ""column_name"", ""direction"": ""asc""|""desc"" }
    ],
    ""limit"": null
  },
  ""styleConfig"": {
    ""variant"": ""one of the variant ids listed for the chosen chartType"",
    ""palette"": ""one of the palette ids listed above"",
    ""params"": { ""paramKey"": value }
  }
}

Rules:
- yAxis columns must be present in the dataModel output: groupBy columns plus aggregated columns. When an aggregation runs on a column, the output keeps the same column name.
- Aggregated outputs reuse the source column name (e.g. SUM of ""amount"" produces a column named ""amount"").
- For count with no obvious column, pick a column from the schema and function ""count"".
- filters/orderBy column names must be from the schema; omit them when no filtering/ordering is meaningful.
- Do NOT invent columns that are not in the schema.
- If the data needs no grouping or aggregation, emit groupBy: [], aggregations: [], filters: [].
- styleConfig.variant must be a variant of the chartType you chose; styleConfig.params may only use the parameter keys listed for that chartType
- Omit styleConfig fields you have no opinion about rather than guessing
- The JSON must be parseable and complete
";

        return template
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__PALETTES__", string.Join(", ", ChartCatalog.Palettes.Select(p => p.Id)))
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")));
    }

    /// <summary>
    /// Renders the catalog as compact text so the prompt always matches what the
    /// renderer and validators support.
    /// </summary>
    private static string DescribeCatalog()
    {
        var lines = new List<string>();

        foreach (var type in ChartCatalog.Types)
        {
            lines.Add($"- {type.Id}: {type.Description}");
            lines.Add($"  variants: {string.Join(", ", type.Variants.Select(v => $"{v.Id} ({v.Description})"))}");
            lines.Add($"  params: {string.Join(", ", type.Params.Select(DescribeParam))}");
        }

        return string.Join("\n", lines);
    }

    private static string DescribeParam(ChartParamSpec param) => param.Kind switch
    {
        ChartParamKind.Boolean => $"{param.Key} (boolean, default {param.Default.ToString()!.ToLowerInvariant()})",
        ChartParamKind.Number =>
            $"{param.Key} (number {param.Min}–{param.Max}, default {param.Default})",
        ChartParamKind.Select =>
            $"{param.Key} (one of {string.Join("|", param.Options!.Select(o => o.Value))}, default {param.Default})",
        _ => param.Key
    };

    private class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    private class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
    }

    private class OpenRouterMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
