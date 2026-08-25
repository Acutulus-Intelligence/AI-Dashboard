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
        IReadOnlyList<string>? allowedColors = null,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(
            schemaJson, dbProvider, prefabChartType, currentChartJson, allowedColors);

        var userContent = string.IsNullOrWhiteSpace(currentChartJson)
            ? prompt
            : BuildRefineUserPrompt(prompt, currentChartJson);

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
        CancellationToken ct = default)
    {
        var systemPrompt = BuildCollectionSystemPrompt(schemaJson, prefabChartType);
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

    private static string BuildRefineUserPrompt(string prompt, string currentChartJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Refine this existing chart. Map the user request onto the allowed fields below. For style-only requests (rounding, prefix/suffix, info, variant) you MUST copy sqlQuery, chartType, axes, aggregation, and groupBy EXACTLY from the current chart JSON — do not rewrite SQL.");
        sb.AppendLine(
            "Series colour slots follow yAxis order (index 0 = first yAxis name). Prefer styleConfig.colors as an object keyed by those names when colouring a specific series/column.");
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
        string? currentChartJson,
        IReadOnlyList<string>? allowedColors)
    {
        var isRefine = !string.IsNullOrWhiteSpace(currentChartJson);
        var colorAllowlist = FormatColorAllowlist(allowedColors);
        var paletteIds = string.Join(", ", ChartCatalog.Palettes.Select(p => $"\"{p.Id}\""));

        var chartPreference = isRefine
            ? "The user is refining an existing chart. Style-only edits must keep sqlQuery identical. Chart-type changes (bar → radar, etc.) are allowed when asked — then update chartType, variant, and SQL/axes as needed. Colours/palette only from the allowlist below; never invent free hex. Never set params."
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
            DbProvider.SqlServer => "SQL Server",
            DbProvider.Sqlite => "SQLite",
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
- Series/column colour (e.g. make amount blue / Colour 5 for price) → styleConfig.colors ONLY (no palette). Prefer a name→colour object keyed by yAxis column names, or an array in yAxis order. Values MUST be exact allowlist strings (the var(--chart-N) or #hex token — never invent hex). Match colour words to the hue hints on the allowlist (purple/lila, red/röd, …). If the user names several colours, assign one allowlist value per series
- Theme palette (cool / warm / default / …) → styleConfig.palette ONLY (no colors)
""";

        var refineRules = isRefine
            ? $"""
{styleVocabulary}
- CRITICAL: Only change styleConfig (colours, palette, prefix/suffix, decimals, info, variant) when the user EXPLICITLY asks for a style/colour/label/variant change. Otherwise copy styleConfig EXACTLY from the current chart — do not invent or "improve" colours/theme
- You may adjust data fields when asked: chartType, title, sqlQuery, axes, aggregation, groupBy
- Style-only (decimals, prefix, suffix, info, variant, colors/palette): copy sqlQuery character-for-character from the current chart — never invent ROUND()/CAST() in SQL for display rounding; display rounding is styleConfig only
- Chart-type switch: set chartType, pick a valid variant for the NEW type, rewrite sqlQuery/axes only when needed; keep colours/labels from the current chart unless they asked to restyle
- NEVER set both styleConfig.palette and styleConfig.colors in the same response — pick exactly one colour mode
- Theme palette request → set palette only; set colors to null / omit colors
- Account/slice/column colour request → set colors only; set palette to null / omit palette
- styleConfig.colors formats (pick one):
  - object keyed by yAxis column/series name → allowlist value (e.g. amount maps to Colour N)
  - array of allowlist values in the same order as yAxis (index 0 = yAxis[0])
- styleConfig.colors values: ONLY exact strings from the account colour allowlist (Colour 1, Colour 2, …). Never invent hex outside that list
- table charts: do NOT set colors, palette, valuePrefix, valueSuffix, or decimals
- Do NOT set customColors or params — the UI controls those
- Prefer changing only what the request implies; copy all other fields EXACTLY from the current chart JSON
- Never invent or request raw data rows — you only receive SQL and style metadata, never query results
- Reply with the complete JSON object only
"""
            : $"""
{styleVocabulary}
- In styleConfig set variant, info, decimals, decimalMode, valuePrefix, valueSuffix, and either colors OR palette when relevant
- NEVER set both styleConfig.palette and styleConfig.colors — pick exactly one colour mode
- Theme palette → palette only; account/slice/column colours → colors only from the allowlist
- styleConfig.colors may be a name→colour object keyed by yAxis names, or an array in yAxis order
- table charts: do NOT set colors, palette, valuePrefix, valueSuffix, or decimals
- Do NOT set customColors or params — the UI controls those
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

Account colours (use ONLY these exact strings in styleConfig.colors — slice/column mode only):
__COLORS__

Available theme palettes (styleConfig.palette — palette mode only): __PALETTES__

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
    ""colors"": null,
    ""palette"": null,
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
- Colour mode XOR: set either palette OR colors, never both; omit the unused field
- When setting colors, prefer { ""yAxisColumn"": ""<allowlist>"" } so each series is explicit; array form must follow yAxis order
- styleConfig.colors must use only allowlisted values; omit colors in palette mode
- Do not include customColors or params in styleConfig
- Omit styleConfig fields you have no opinion about rather than guessing
__REFINE_RULES__
- The JSON must be parseable and complete
";

        return template
            .Replace("__DBNAME__", dbName)
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__COLORS__", colorAllowlist)
            .Replace("__PALETTES__", paletteIds)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")))
            .Replace("__QUOTING_RULE__", quotingRule)
            .Replace("__REFINE_RULES__", refineRules);
    }

    private static string FormatColorAllowlist(IReadOnlyList<string>? allowedColors)
    {
        if (allowedColors is null || allowedColors.Count == 0)
            return "(none — do not set styleConfig.colors)";

        var lines = allowedColors
            .Select((c, i) => $"- Colour {i + 1}: {DescribeAllowlistColor(c)}")
            .ToArray();
        return string.Join(
            "\n",
            lines.Append(
                "Use the exact token/hex string after the colon as styleConfig.colors values (not the Colour N label, not the hue word). Match user colour words (purple/lila, red/röd, blue/blå, …) to the closest hue hint above. When they ask for several colours, set one allowlist value per series/column."));
    }

    /// <summary>
    /// Theme tokens are opaque to the model without a hue hint; hex swatches speak for themselves.
    /// </summary>
    private static string DescribeAllowlistColor(string color)
    {
        var trimmed = color.Trim();
        if (trimmed.StartsWith('#') && trimmed.Length is >= 4 and <= 9)
            return $"{trimmed} (custom hex)";

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"^var\(\s*--chart-([1-8])\s*\)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (tokenMatch.Success
            && ThemeChartHueHints.TryGetValue(tokenMatch.Groups[1].Value, out var hint))
        {
            return $"{trimmed} — {hint}";
        }

        return trimmed;
    }

    /// <summary>
    /// Approximate light-theme hues for default <c>--chart-N</c> tokens (see Frontend index.css).
    /// </summary>
    private static readonly Dictionary<string, string> ThemeChartHueHints = new(StringComparer.Ordinal)
    {
        ["1"] = "blue",
        ["2"] = "orange",
        ["3"] = "green",
        ["4"] = "purple / lila",
        ["5"] = "yellow / gold",
        ["6"] = "red / röd",
        ["7"] = "teal / cyan",
        ["8"] = "violet",
    };

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
    /// Compact catalog: types + variants only (params are UI-controlled).
    /// </summary>
    private static string DescribeCatalog()
    {
        var lines = new List<string>();

        foreach (var type in ChartCatalog.Types)
        {
            lines.Add($"- {type.Id}: {type.Description}");
            lines.Add($"  variants: {string.Join(", ", type.Variants.Select(v => $"{v.Id} ({v.Description})"))}");
            var styleBits = new List<string>();
            if (type.SupportsColors) styleBits.Add("colors/palette");
            if (type.SupportsValueFormat) styleBits.Add("prefix/suffix/decimals");
            styleBits.Add("info");
            styleBits.Add("variant");
            lines.Add($"  style: {string.Join(", ", styleBits)}"
                + (type.SupportsColors ? "" : " (no colours)"));
        }

        return string.Join("\n", lines);
    }

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