using System.Net.Http.Json;
using System.Text;
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

    public async Task<AiChartResult> GenerateChartConfigAsync(
        string schemaJson,
        string prompt,
        DbProvider dbProvider,
        string? prefabChartType = null,
        string? currentChartJson = null,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(
            schemaJson, dbProvider, prefabChartType, currentChartJson);

        var userContent = string.IsNullOrWhiteSpace(currentChartJson)
            ? prompt
            : BuildRefineUserPrompt(prompt, currentChartJson);

        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            response_format = new { type = "json_object" },
            max_tokens = Math.Max(_settings.MaxTokens, 2048),
            temperature = _settings.Temperature,
            // Keep reasoning light so chart JSON is not starved of the token budget.
            reasoning = new { effort = "low" },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/Acutulus-Intelligence/AI-Dashboard");
        request.Headers.Add("X-OpenRouter-Title", "AI-Dashboard");

        var response = await _httpClient.SendAsync(request, ct);
        var rawResponse = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AI provider returned {Status}: {Body}",
                (int)response.StatusCode,
                Truncate(rawResponse, 500));
            throw new InvalidOperationException(
                $"AI provider error ({(int)response.StatusCode}): {Truncate(rawResponse, 240)}");
        }

        OpenRouterResponse? responseBody;
        try
        {
            responseBody = JsonSerializer.Deserialize<OpenRouterResponse>(rawResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI provider returned non-JSON body: {Body}", Truncate(rawResponse, 500));
            throw new InvalidOperationException(
                $"AI provider returned an invalid response. {Truncate(rawResponse, 240)}");
        }

        var choice = responseBody?.Choices?.FirstOrDefault();
        var finishReason = choice?.FinishReason ?? choice?.NativeFinishReason;
        var content = ExtractMessageText(choice?.Message);
        if (string.IsNullOrWhiteSpace(content))
        {
            var finish = finishReason ?? "unknown";
            _logger.LogWarning(
                "AI empty content. finish_reason={Finish}, body={Body}",
                finish,
                Truncate(rawResponse, 800));

            if (string.Equals(finish, "length", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "AI ran out of tokens before finishing the chart JSON (often when changing chart type). Try again, or raise Ai:MaxTokens.");
            }

            throw new InvalidOperationException(
                "AI returned an empty response. Try the adjustment again with a shorter prompt.");
        }

        var json = ExtractJsonObject(content);
        if (_settings.LogResponses)
        {
            _logger.LogInformation(
                "AI raw chart JSON (finish={Finish}): {Json}",
                finishReason ?? "n/a",
                Truncate(json, 4000));
        }

        AiChartConfig config;
        try
        {
            config = ParseAiChartConfig(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "AI chart JSON parse failed. Content={Content}", Truncate(content, 800));
            throw new InvalidOperationException(
                "AI returned invalid chart JSON. Try the adjustment again with a shorter prompt.");
        }

        if (string.IsNullOrEmpty(config.ChartType) || string.IsNullOrEmpty(config.SqlQuery))
            throw new InvalidOperationException("AI response is missing required fields (chartType, sqlQuery).");

        if (!ChartCatalog.IsKnownType(config.ChartType))
            throw new InvalidOperationException(
                $"AI returned unsupported chart type '{config.ChartType}'. " +
                $"Supported types: {string.Join(", ", ChartCatalog.TypeIds)}.");

        // Models routinely invent variants and parameters, so keep only what the
        // catalog actually describes rather than trusting the response.
        try
        {
            config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Style sanitization failed; dropping styleConfig");
            config.StyleConfig = null;
        }

        _logger.LogInformation(
            "AI chart config generated: chartType={ChartType}, sqlLength={SqlLength}, refine={IsRefine}, style={Style}",
            config.ChartType,
            config.SqlQuery.Length,
            !string.IsNullOrWhiteSpace(currentChartJson),
            config.StyleConfig is null
                ? "(none)"
                : JsonSerializer.Serialize(config.StyleConfig));

        return new AiChartResult
        {
            Config = config,
            RawJson = json,
            FinishReason = finishReason,
        };
    }

    /// <summary>
    /// OpenRouter may return content as a string or as an array of text parts.
    /// </summary>
    private static string? ExtractMessageText(OpenRouterMessage? message)
    {
        if (message?.Content is not { } el || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (el.ValueKind == JsonValueKind.String)
            return el.GetString();

        if (el.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in el.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                {
                    sb.Append(part.GetString());
                    continue;
                }

                if (part.ValueKind != JsonValueKind.Object)
                    continue;

                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    sb.Append(text.GetString());
                else if (part.TryGetProperty("content", out var nested) && nested.ValueKind == JsonValueKind.String)
                    sb.Append(nested.GetString());
            }

            var joined = sb.ToString();
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        return null;
    }

    /// <summary>
    /// Parse chart JSON tolerantly: required fields must succeed; a bad styleConfig
    /// is dropped instead of failing the whole generation.
    /// </summary>
    internal static AiChartConfig ParseAiChartConfig(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("AI response root must be a JSON object.");

        var config = new AiChartConfig
        {
            ChartType = GetString(root, "chartType") ?? string.Empty,
            Title = GetString(root, "title") ?? string.Empty,
            XAxis = GetString(root, "xAxis") ?? string.Empty,
            YAxis = GetStringArray(root, "yAxis"),
            Aggregation = GetString(root, "aggregation") ?? "none",
            GroupBy = GetString(root, "groupBy"),
            SqlQuery = GetString(root, "sqlQuery") ?? string.Empty,
        };

        if (root.TryGetProperty("styleConfig", out var styleEl)
            && styleEl.ValueKind == JsonValueKind.Object)
        {
            try
            {
                config.StyleConfig = styleEl.Deserialize<ChartStyleConfig>();
            }
            catch (JsonException)
            {
                // e.g. params as array/boolean — keep the chart, drop broken style
                config.StyleConfig = null;
            }
        }

        return config;
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.Null => null,
            _ => el.ToString()
        };
    }

    private static List<string> GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
            else if (item.ValueKind != JsonValueKind.Null)
            {
                list.Add(item.ToString());
            }
        }
        return list;
    }

    /// <summary>
    /// Models sometimes wrap JSON in prose or markdown fences ("The result is: ```json ...").
    /// Pull out the first JSON object so deserialization does not fail on leading 'T'/'`'.
    /// </summary>
    internal static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();

        // Strip common markdown fences.
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = trimmed.IndexOf('\n');
            if (firstNl >= 0)
                trimmed = trimmed[(firstNl + 1)..];
            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
                trimmed = trimmed[..fence];
            trimmed = trimmed.Trim();
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private static string BuildRefineUserPrompt(string prompt, string currentChartJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Refine this existing chart. Map the user request onto the allowed fields below. For style-only requests (rounding, prefix/suffix, info, variant) you MUST copy sqlQuery, chartType, axes, aggregation, and groupBy EXACTLY from the current chart JSON — do not rewrite SQL.");
        sb.AppendLine(prompt);
        sb.AppendLine();
        sb.AppendLine("Current chart configuration (metadata only — no query result rows):");
        sb.AppendLine(currentChartJson);
        return sb.ToString();
    }

    private static string BuildSystemPrompt(
        string schemaJson,
        DbProvider dbProvider,
        string? prefabChartType,
        string? currentChartJson)
    {
        var isRefine = !string.IsNullOrWhiteSpace(currentChartJson);

        var chartPreference = isRefine
            ? "The user is refining an existing chart. Style-only edits must keep sqlQuery identical. Chart-type changes (bar → radar, etc.) are allowed when asked — then update chartType, variant, and SQL/axes as needed. Never set colors, palette, or params."
            : prefabChartType switch
            {
                not null => $"The user prefers the chart type: {prefabChartType}.",
                null => "Choose the best chart type based on the data."
            };

        var quotingRule = SqlIdentifierQuoter.GetQuotingRule(dbProvider);
        var dbName = dbProvider switch
        {
            DbProvider.PostgreSql => "PostgreSQL",
            DbProvider.MySql => "MySQL",
            _ => "SQL"
        };

        var styleVocabulary = """
Style field vocabulary — use these exact values (Swedish or English user wording maps here):
- Round to integer / heltal / whole number / avrunda till heltal → "decimals": 0, "decimalMode": "round"
- N decimal places / N decimaler → "decimals": N (0–10), "decimalMode": "round" (or "truncate" if they say truncate/klipp)
- Dollar suffix / suffix $ / dollar-tecken → "valueSuffix": "$"
- Dollar prefix / prefix $ → "valuePrefix": "$"
- Krona / kr suffix → "valueSuffix": " kr"
- Percent suffix → "valueSuffix": "%"
- Clear prefix/suffix → set the field to "" 
- Info text → "info": "…"
- Variant / stacked / horizontal / grouped → styleConfig.variant (catalog id only; "grouped"/"grouperad" → "default")
""";

        var refineRules = isRefine
            ? $"""
{styleVocabulary}
- You may adjust: chartType (catalog id), title, sqlQuery, axes, aggregation, groupBy, and styleConfig.variant / info / decimals / decimalMode / valuePrefix / valueSuffix
- Style-only (decimals, prefix, suffix, info, variant): copy sqlQuery character-for-character from the current chart — never invent ROUND()/CAST() in SQL for display rounding; display rounding is styleConfig only
- Chart-type switch: set chartType, pick a valid variant for the NEW type, rewrite sqlQuery/axes only when needed
- Do NOT set styleConfig.colors, palette, customColors, or params — the UI controls those
- Prefer changing only what the request implies; copy all other fields EXACTLY from the current chart JSON
- Never invent or request raw data rows — you only receive SQL and style metadata, never query results
- Reply with the complete JSON object only
"""
            : $"""
{styleVocabulary}
- In styleConfig only set variant, info, decimals, decimalMode, valuePrefix, valueSuffix when relevant
- Do NOT set colors, palette, customColors, or params — the UI controls those
- Display formatting (rounding, $, %) belongs in styleConfig — not in SQL
""";

        var template = @"
You are a data visualization assistant. Given a database table schema, generate a chart configuration.
Your entire reply MUST be a single raw JSON object that starts with { and ends with }.
No markdown, no code fences, no prose before or after the JSON.

Database: __DBNAME__
Table schema:
__SCHEMA__

__PREFERENCE__

Available chart types and variants:
__CATALOG__

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
    ""valuePrefix"": ""optional string"",
    ""valueSuffix"": ""optional string"",
    ""decimals"": null,
    ""decimalMode"": ""round"" | ""truncate"" | null,
    ""info"": ""optional short info text""
  }
}

Rules:
- sqlQuery must be a valid SELECT query only for __DBNAME__
- __QUOTING_RULE__
- Never include actual data values — only column names and SQL
- styleConfig.variant must be a variant of the chartType you chose
- Do not include colors, palette, customColors, or params in styleConfig
- Omit styleConfig fields you have no opinion about rather than guessing
__REFINE_RULES__
- The JSON must be parseable and complete
";

        return template
            .Replace("__DBNAME__", dbName)
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")))
            .Replace("__QUOTING_RULE__", quotingRule)
            .Replace("__REFINE_RULES__", refineRules);
    }

    /// <summary>
    /// Compact catalog: types + variants only (params are UI-controlled).
    /// </summary>
    private static string DescribeCatalog()
    {
        var lines = new List<string>();

        foreach (var type in ChartCatalog.Types)
        {
            lines.Add($"- {type.Id}: {type.Description}");
            lines.Add($"  variants: {string.Join(", ", type.Variants.Select(v => $"{v.Id} ({v.Description})"))}");
        }

        return string.Join("\n", lines);
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

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("native_finish_reason")]
        public string? NativeFinishReason { get; set; }
    }

    private class OpenRouterMessage
    {
        /// <summary>String or array of content parts depending on the model.</summary>
        [JsonPropertyName("content")]
        public JsonElement? Content { get; set; }
    }
}
