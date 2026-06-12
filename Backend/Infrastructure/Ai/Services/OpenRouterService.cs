using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai.Services;

public class OpenRouterService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(HttpClient httpClient, IOptions<AiSettings> settings, ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiChartConfig> GenerateChartConfigAsync(string schemaJson, string prompt, string? prefabChartType = null, CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(schemaJson, prefabChartType);

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

        _logger.LogInformation("AI response: {Response}", content);

        var config = JsonSerializer.Deserialize<AiChartConfig>(content)
            ?? throw new InvalidOperationException("Failed to parse AI response as chart config.");

        if (string.IsNullOrEmpty(config.ChartType) || string.IsNullOrEmpty(config.SqlQuery))
            throw new InvalidOperationException("AI response is missing required fields (chartType, sqlQuery).");

        return config;
    }

    private static string BuildSystemPrompt(string schemaJson, string? prefabChartType)
    {
        var chartPreference = prefabChartType switch
        {
            not null => $"The user prefers the chart type: {prefabChartType}.",
            null => "Choose the best chart type based on the data."
        };

        var template = @"
You are a data visualization assistant. Given a database table schema, generate a chart configuration.
Return ONLY valid JSON — no markdown, no code fences, no extra text.

Table schema:
__SCHEMA__

__PREFERENCE__

Return this exact JSON structure:
{
  ""chartType"": ""bar"" | ""line"" | ""pie"" | ""area"" | ""scatter"",
  ""title"": ""string — concise chart title"",
  ""xAxis"": ""column_name — the column for the x-axis / labels"",
  ""yAxis"": [""column_name — one or more columns for the y-axis / values""],
  ""aggregation"": ""sum"" | ""avg"" | ""count"" | ""min"" | ""max"" | ""none"",
  ""groupBy"": ""column_name | null — column to group by, or null"",
  ""sqlQuery"": ""SELECT ... — a safe, valid SELECT query that fetches the data needed""
}

Rules:
- sqlQuery must be a valid SELECT query only
- Use double quotes for table and column names
- Never include actual data values — only column names and SQL
- The JSON must be parseable and complete
";

        return template
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__PREFERENCE__", chartPreference);
    }

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
