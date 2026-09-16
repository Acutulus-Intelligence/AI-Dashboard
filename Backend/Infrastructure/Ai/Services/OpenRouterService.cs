using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces;
using Application.Services;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
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
        IReadOnlyList<string>? allowedColors = null,
        CancellationToken ct = default)
    {
        var systemPrompt = AiChartPromptBuilder.BuildSystemPrompt(
            schemaJson, dbProvider, prefabChartType, currentChartJson, allowedColors);

        var userContent = string.IsNullOrWhiteSpace(currentChartJson)
            ? prompt
            : AiChartPromptBuilder.BuildRefineUserPrompt(prompt, currentChartJson);

        var result = await SendChatAsync(systemPrompt, userContent, ct);
        var config = result.Config;
        var isRefine = !string.IsNullOrWhiteSpace(currentChartJson);

        // First generate needs chartType + sqlQuery. On refine the model often omits
        // unchanged fields — ChartRefineMerger fills those from the baseline.
        if (!isRefine && (string.IsNullOrEmpty(config.ChartType) || string.IsNullOrEmpty(config.SqlQuery)))
        {
            throw new InvalidOperationException(
                "AI response is missing required fields (chartType, sqlQuery). " +
                $"Raw: {Truncate(result.Json, 400)}");
        }

        if (!string.IsNullOrEmpty(config.ChartType) && !ChartCatalog.IsKnownType(config.ChartType))
            throw new InvalidOperationException(
                $"AI returned unsupported chart type '{config.ChartType}'. " +
                $"Supported types: {string.Join(", ", ChartCatalog.TypeIds)}. " +
                $"Raw: {Truncate(result.Json, 400)}");

        // Models routinely invent variants and parameters, so keep only what the
        // catalog actually describes rather than trusting the response.
        if (!string.IsNullOrEmpty(config.ChartType))
        {
            try
            {
                config.StyleConfig = ChartStyleSanitizer.Sanitize(config.StyleConfig, config.ChartType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Style sanitization failed; dropping styleConfig");
                config.StyleConfig = null;
            }
        }

        _logger.LogInformation(
            "AI chart config generated: chartType={ChartType}, sqlLength={SqlLength}, refine={IsRefine}, style={Style}",
            string.IsNullOrEmpty(config.ChartType) ? "(omitted)" : config.ChartType,
            config.SqlQuery?.Length ?? 0,
            isRefine,
            config.StyleConfig is null
                ? "(none)"
                : JsonSerializer.Serialize(config.StyleConfig));

        return new AiChartResult
        {
            Config = config,
            RawJson = result.Json,
            FinishReason = result.FinishReason,
        };
    }

    public async Task<AiChartConfig> GenerateCollectionChartConfigAsync(
        string schemaJson,
        string prompt,
        string? prefabChartType = null,
        IReadOnlyList<string>? allowedColors = null,
        CancellationToken ct = default)
    {
        var systemPrompt = AiChartPromptBuilder.BuildCollectionSystemPrompt(
            schemaJson, prefabChartType, allowedColors);
        var config = (await SendChatAsync(systemPrompt, prompt, ct)).Config;

        if (string.IsNullOrEmpty(config.ChartType) || config.DataModel is null)
            throw new InvalidOperationException("AI response is missing required fields (chartType, dataModel).");

        config.SqlQuery = string.Empty;
        return FinalizeConfig(config);
    }

    /// <summary>
    /// Sends a prompt to OpenRouter and tolerantly parses the chart JSON out of the reply.
    /// </summary>
    private async Task<AiChatOutcome> SendChatAsync(string systemPrompt, string userContent, CancellationToken ct)
    {
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

        return new AiChatOutcome(config, json, finishReason);
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
            config.SqlQuery?.Length ?? 0);

        return config;
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
            Aggregation = GetString(root, "aggregation") ?? string.Empty,
            GroupBy = GetString(root, "groupBy"),
            SqlQuery = GetString(root, "sqlQuery") ?? string.Empty,
        };

        if (TryGetPropertyIgnoreCase(root, "styleConfig", out var styleEl)
            && styleEl.ValueKind == JsonValueKind.Object)
        {
            try
            {
                config.StyleConfig = ParseStyleConfig(styleEl, config);
            }
            catch (JsonException)
            {
                // e.g. params as array/boolean — keep the chart, drop broken style
                config.StyleConfig = null;
                config.NamedColorMap = null;
            }
        }

        if (TryGetPropertyIgnoreCase(root, "dataModel", out var dataModelEl)
            && dataModelEl.ValueKind == JsonValueKind.Object)
        {
            try
            {
                config.DataModel = dataModelEl.Deserialize<DataQueryModel>();
            }
            catch (JsonException)
            {
                // Tolerant like styleConfig: a broken dataModel surfaces downstream.
                config.DataModel = null;
            }
        }

        return config;
    }

    /// <summary>
    /// Parses styleConfig; <c>colors</c> may be a positional array or a name→colour object.
    /// </summary>
    private static ChartStyleConfig? ParseStyleConfig(JsonElement styleEl, AiChartConfig config)
    {
        using var doc = JsonDocument.Parse(styleEl.GetRawText());
        var root = doc.RootElement;

        JsonElement? colorsEl = null;
        if (TryGetPropertyIgnoreCase(root, "colors", out var colorsProp)
            && colorsProp.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            colorsEl = colorsProp.Clone();
        }

        // Rebuild style JSON without colours so an object-shaped colors value cannot fail deserialize.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.Equals("colors", StringComparison.OrdinalIgnoreCase))
                    continue;
                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        var strippedJson = Encoding.UTF8.GetString(stream.ToArray());
        var style = JsonSerializer.Deserialize<ChartStyleConfig>(strippedJson) ?? new ChartStyleConfig();

        if (colorsEl is { } colors)
        {
            if (colors.ValueKind == JsonValueKind.Array)
            {
                style.Colors = ParseColorArray(colors);
                config.NamedColorMap = null;
            }
            else if (colors.ValueKind == JsonValueKind.Object)
            {
                var map = ParseColorObject(colors);
                config.NamedColorMap = map;
                // Expanded in GraphGenerationService once yAxis (possibly from baseline) is known.
                style.Colors = null;
                if (map is { Count: > 0 })
                    style.Palette = null;
            }
        }

        return style;
    }

    private static List<string>? ParseColorArray(JsonElement colors)
    {
        var list = new List<string>();
        foreach (var item in colors.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                list.Add(item.GetString() ?? string.Empty);
            else if (item.ValueKind == JsonValueKind.Null)
                list.Add(string.Empty);
            else
                list.Add(item.ToString());
        }

        while (list.Count > 0 && string.IsNullOrWhiteSpace(list[^1]))
            list.RemoveAt(list.Count - 1);

        return list.Exists(c => !string.IsNullOrWhiteSpace(c)) ? list : null;
    }

    private static Dictionary<string, string>? ParseColorObject(JsonElement colors)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in colors.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            var value = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(prop.Name) || string.IsNullOrWhiteSpace(value)) continue;
            map[prop.Name.Trim()] = value.Trim();
        }

        return map.Count > 0 ? map : null;
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!TryGetPropertyIgnoreCase(root, name, out var el)) return null;
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
        if (!TryGetPropertyIgnoreCase(root, name, out var el) || el.ValueKind != JsonValueKind.Array)
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

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
            return true;

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
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

    private sealed record AiChatOutcome(AiChartConfig Config, string Json, string? FinishReason);

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