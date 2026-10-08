using System.Text;
using System.Text.Json;
using Domain.Charts;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// System/user prompt construction for chart generation. Lives in Application so
/// allowlists and anti-hallucination rules can be unit-tested without HTTP.
/// </summary>
public static class AiChartPromptBuilder
{
    public static string QuotingRule(DbProvider provider) => provider switch
    {
        DbProvider.PostgreSql => "Use double quotes for table and column names (e.g. \"table_name\")",
        DbProvider.MySql => "Use backticks for table and column names (e.g. `table_name`)",
        DbProvider.SqlServer => "Use square brackets for table and column names (e.g. [table_name])",
        _ => "Quote identifiers appropriately for the database",
    };

    public static string AppendRepairHint(string userContent, string? repairHint)
    {
        if (string.IsNullOrWhiteSpace(repairHint))
            return userContent;

        return
            """
            Your previous JSON was rejected because it invented or misused schema/style values.
            Return a corrected JSON object. Do not invent tables, columns, chart types, variants, palettes, or colours.
            """
            + $"\nValidation error: {repairHint}\n\nOriginal request:\n{userContent}";
    }

    public static string BuildRefineUserPrompt(string prompt, string currentChartJson)
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

    public static string BuildSystemPrompt(
        string schemaJson,
        DbProvider dbProvider,
        string? prefabChartType,
        string? currentChartJson,
        IReadOnlyList<string>? allowedColors)
    {
        var isRefine = !string.IsNullOrWhiteSpace(currentChartJson);
        var colorAllowlist = FormatColorAllowlist(allowedColors);
        var paletteIds = string.Join(", ", ChartCatalog.Palettes.Select(p => $"\"{p.Id}\""));
        var requestedType = ChartCatalog.CanonicalId(prefabChartType);

        var chartPreference = isRefine
            ? "The user is refining an existing chart. Style-only edits must keep sqlQuery identical. Chart-type changes (bar → radar, etc.) are allowed when asked — then update chartType, variant, and SQL/axes as needed. Colours/palette only from the allowlist below; never invent free hex. Never set params."
            : requestedType switch
            {
                not null => $"The user prefers the chart type: {requestedType}.",
                null => "Choose the best chart type based on the data."
            };

        var quotingRule = QuotingRule(dbProvider);
        var dbName = dbProvider switch
        {
            DbProvider.PostgreSql => "PostgreSQL",
            DbProvider.MySql => "MySQL",
            DbProvider.SqlServer => "SQL Server",
            DbProvider.Sqlite => "SQLite",
            _ => "SQL"
        };
        var schemaBlock = FormatSchemaGrounding(schemaJson);

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
- CRITICAL: sqlQuery may ONLY use tables in allowedTables and columns in allowedColumnNames. Never invent, rename, guess, or pluralize column names. Aliases are allowed; their source columns must still be listed.
- If the user asks for a field that is not in allowedColumnNames, pick the closest listed column and reflect that in the title — never fabricate a column
""";

        var template = @"
You are a data visualization assistant. Given a database table schema, generate a chart configuration.
Your entire reply MUST be a single raw JSON object that starts with { and ends with }.
No markdown, no code fences, no prose before or after the JSON.

Database: __DBNAME__
__SCHEMA__

__PREFERENCE__

Account colours (use ONLY these exact strings in styleConfig.colors — slice/column mode only):
__COLORS__

Available theme palettes (styleConfig.palette — palette mode only): __PALETTES__

Available chart types and variants:
__CATALOG__

Return this exact JSON structure (no additional properties):
{
  ""chartType"": __TYPE_UNION__,
  ""title"": ""string — concise chart title"",
  ""xAxis"": ""column_name — MUST be an allowedColumnName or a SELECT alias"",
  ""yAxis"": [""column_name — MUST be an allowedColumnName or a SELECT alias""],
  ""aggregation"": ""sum"" | ""avg"" | ""count"" | ""min"" | ""max"" | ""none"",
  ""groupBy"": ""column_name | null — MUST be an allowedColumnName or null"",
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
- SCHEMA GROUNDING: Never invent tables or columns. sqlQuery, xAxis, yAxis, and groupBy may only use allowedTables / allowedColumnNames (plus SELECT aliases whose sources are allowed columns).
- Never JOIN a table that is not in allowedTables
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
            .Replace("__SCHEMA__", schemaBlock)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__COLORS__", colorAllowlist)
            .Replace("__PALETTES__", paletteIds)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")))
            .Replace("__QUOTING_RULE__", quotingRule)
            .Replace("__REFINE_RULES__", refineRules);
    }

    /// <summary>
    /// Turns the structured schema JSON into a hard-to-miss allowlist the model must obey.
    /// </summary>
    public static string FormatSchemaGrounding(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            var sb = new StringBuilder();

            var table = GetString(root, "table") ?? GetString(root, "allowedTables");
            sb.AppendLine("Connected schema (metadata only — no row data):");
            sb.AppendLine(schemaJson);
            sb.AppendLine();
            sb.AppendLine("ALLOWED TABLES (the ONLY tables you may query):");
            if (TryGetPropertyIgnoreCase(root, "allowedTables", out var tablesEl)
                && tablesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tablesEl.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
                        sb.AppendLine($"- {t.GetString()}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(table))
            {
                sb.AppendLine($"- {table}");
            }

            sb.AppendLine();
            sb.AppendLine("ALLOWED COLUMNS (the ONLY columns you may reference — never invent names):");
            if (TryGetPropertyIgnoreCase(root, "columns", out var colsEl)
                && colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var col in colsEl.EnumerateArray())
                {
                    if (col.ValueKind != JsonValueKind.Object) continue;
                    var name = GetString(col, "name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var type = GetString(col, "type") ?? "unknown";
                    var nullable = col.TryGetProperty("nullable", out var n) && n.ValueKind is JsonValueKind.True
                        ? "nullable"
                        : "not null";
                    var hint = GetString(col, "usageHint");
                    sb.AppendLine(string.IsNullOrWhiteSpace(hint)
                        ? $"- {name} ({type}, {nullable})"
                        : $"- {name} ({type}, {nullable}, {hint})");
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                "If a requested metric cannot be computed from these columns, use the closest listed columns and say so in the title. Never fabricate a column or table name.");
            return sb.ToString();
        }
        catch (JsonException)
        {
            return "Table schema:\n" + schemaJson;
        }
    }

    public static string FormatColorAllowlist(IReadOnlyList<string>? allowedColors)
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

    public static string BuildCollectionSystemPrompt(
        string schemaJson,
        string? prefabChartType,
        IReadOnlyList<string>? allowedColors = null)
    {
        var requestedType = ChartCatalog.CanonicalId(prefabChartType);
        var chartPreference = requestedType switch
        {
            not null => $"The user prefers the chart type: {requestedType}.",
            null => "Choose the best chart type based on the data."
        };
        var schemaBlock = FormatSchemaGrounding(schemaJson);
        var colorAllowlist = FormatColorAllowlist(allowedColors);

        var template = @"
You are a data visualization assistant. Given the schema of uploaded tabular data, generate a chart configuration.
Return ONLY valid JSON — no markdown, no code fences, no extra text.

__SCHEMA__

__PREFERENCE__

Account colours (use ONLY these exact strings in styleConfig.colors — slice/column mode only):
__COLORS__

Available chart types, their variants and their adjustable parameters:
__CATALOG__

Available palettes: __PALETTES__

This data lives in memory, not in a database. Do NOT generate SQL — instead build a structured query (dataModel) that is applied to the rows in memory.

Return this exact JSON structure (no additional properties):
{
  ""chartType"": __TYPE_UNION__,
  ""title"": ""string — concise chart title"",
  ""xAxis"": ""column_name — MUST be an allowedColumnName"",
  ""yAxis"": [""column_name — MUST be an allowedColumnName present in the dataModel output""],
  ""aggregation"": ""sum"" | ""avg"" | ""count"" | ""min"" | ""max"" | ""none"",
  ""groupBy"": ""column_name | null — MUST be an allowedColumnName or null"",
  ""dataModel"": {
    ""filters"": [
      { ""column"": ""column_name"", ""operator"": ""eq""|""neq""|""gt""|""gte""|""lt""|""lte""|""contains""|""in""|""notin""|""isnull""|""isnotnull"", ""value"": ""string — raw filter value, e.g. a category name, number, or comma-separated list for in/notin"" }
    ],
    ""groupBy"": [""column_name — one or more group columns from allowedColumnNames""],
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
    ""colors"": null,
    ""valuePrefix"": ""optional string"",
    ""valueSuffix"": ""optional string"",
    ""decimals"": null,
    ""decimalMode"": ""round"" | ""truncate"" | null,
    ""info"": ""optional short info text""
  }
}

Rules:
- yAxis columns must be present in the dataModel output: groupBy columns plus aggregated columns. When an aggregation runs on a column, the output keeps the same column name.
- Aggregated outputs reuse the source column name (e.g. SUM of ""amount"" produces a column named ""amount"").
- For count with no obvious column, pick a column from allowedColumnNames and function ""count"".
- filters/orderBy/groupBy/aggregations column names MUST be from allowedColumnNames; omit them when no filtering/ordering is meaningful.
- CRITICAL: Do NOT invent columns that are not in allowedColumnNames. Never rename, guess, or pluralize column names.
- If the data needs no grouping or aggregation, emit groupBy: [], aggregations: [], filters: [].
- styleConfig.variant must be a variant of the chartType you chose
- Do NOT set customColors or params — the UI controls those
- Omit styleConfig fields you have no opinion about rather than guessing
- The JSON must be parseable and complete
";

        return template
            .Replace("__SCHEMA__", schemaBlock)
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__COLORS__", colorAllowlist)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__PALETTES__", string.Join(", ", ChartCatalog.Palettes.Select(p => p.Id)))
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")));
    }

    /// <summary>
    /// Compact catalog: types + variants only (params are UI-controlled).
    /// </summary>
    public static string DescribeCatalog()
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
}
