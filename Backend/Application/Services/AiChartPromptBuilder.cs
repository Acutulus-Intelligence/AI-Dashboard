using System.Text;
using Domain.Charts;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// System/user prompts for chart SQL and collection generation. Kept in Application
/// so grounding rules and allowlists can be unit-tested without calling the provider.
/// </summary>
public static class AiChartPromptBuilder
{
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

        var chartPreference = isRefine
            ? "The user is refining an existing chart. Style-only edits must keep sqlQuery identical. Chart-type changes (bar → radar, etc.) are allowed when asked — then update chartType, variant, and SQL/axes as needed. Colours/palette only from the allowlist below; never invent free hex. Never set params."
            : prefabChartType switch
            {
                not null => $"The user prefers the chart type: {prefabChartType}.",
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
- Do NOT set customColors or params — the UI controls those. Invented params are discarded
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
- Do NOT set customColors or params — the UI controls those. Invented params are discarded
- Display formatting (rounding, $, %) belongs in styleConfig — not in SQL
""";

        var template = @"
You are a data visualization assistant. Given a database table schema, generate a chart configuration.
Your entire reply MUST be a single raw JSON object that starts with { and ends with }.
No markdown, no code fences, no prose before or after the JSON.

Database: __DBNAME__
Table schema (metadata only — no row data):
__SCHEMA__

Identifier allowlist (hard constraints — violating these is a failure):
__ALLOWLIST__

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
- sqlQuery, xAxis, yAxis, and groupBy may use ONLY names from the identifier allowlist (or SELECT aliases of those columns)
- Do not JOIN or reference any table that is not in the allowlist
- Never invent columns such as customer_name, revenue, or created_at unless they appear in allowedColumns
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
            .Replace("__ALLOWLIST__", FormatIdentifierAllowlist(schemaJson))
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__COLORS__", colorAllowlist)
            .Replace("__PALETTES__", paletteIds)
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")))
            .Replace("__QUOTING_RULE__", quotingRule)
            .Replace("__REFINE_RULES__", refineRules);
    }

    public static string BuildCollectionSystemPrompt(
        string schemaJson,
        string? prefabChartType,
        IReadOnlyList<string>? allowedColors = null)
    {
        var chartPreference = prefabChartType switch
        {
            not null => $"The user prefers the chart type: {prefabChartType}.",
            null => "Choose the best chart type based on the data."
        };

        var template = @"
You are a data visualization assistant. Given the schema of uploaded tabular data, generate a chart configuration.
Return ONLY valid JSON — no markdown, no code fences, no extra text.

Data schema (columns and their inferred types; metadata only — no row data):
__SCHEMA__

Identifier allowlist (hard constraints — violating these is a failure):
__ALLOWLIST__

__PREFERENCE__

Account colours (use ONLY these exact strings in styleConfig.colors — slice/column mode only):
__COLORS__

Available chart types, their variants and AI-controlled style fields:
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
- yAxis columns must be present in the dataModel output: groupBy columns plus aggregated columns. When an aggregation runs on a column, the output keeps the same column name.
- Aggregated outputs reuse the source column name (e.g. SUM of ""amount"" produces a column named ""amount"").
- For count with no obvious column, pick a column from the schema and function ""count"".
- filters/orderBy/groupBy/aggregations column names MUST be from the identifier allowlist; omit them when no filtering/ordering is meaningful.
- Do NOT invent columns that are not in the schema.
- If the data needs no grouping or aggregation, emit groupBy: [], aggregations: [], filters: [].
- styleConfig.variant must be a variant of the chartType you chose
- NEVER set both styleConfig.palette and styleConfig.colors — pick exactly one colour mode
- Do NOT set customColors or params — the UI controls those. Invented params are discarded
- Omit styleConfig fields you have no opinion about rather than guessing
- The JSON must be parseable and complete
";

        return template
            .Replace("__SCHEMA__", schemaJson)
            .Replace("__ALLOWLIST__", FormatIdentifierAllowlist(schemaJson))
            .Replace("__PREFERENCE__", chartPreference)
            .Replace("__COLORS__", FormatColorAllowlist(allowedColors))
            .Replace("__CATALOG__", DescribeCatalog())
            .Replace("__PALETTES__", string.Join(", ", ChartCatalog.Palettes.Select(p => $"\"{p.Id}\"")))
            .Replace("__TYPE_UNION__", string.Join(" | ", ChartCatalog.TypeIds.Select(id => $"\"{id}\"")));
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
                + (type.SupportsColors ? "" : " (no colours)")
                + " — never set params");
        }

        return string.Join("\n", lines);
    }

    public static string QuotingRule(DbProvider provider) => provider switch
    {
        DbProvider.PostgreSql => "Use double quotes for table and column names (e.g. \"table_name\")",
        DbProvider.MySql => "Use backticks for table and column names (e.g. `table_name`)",
        DbProvider.SqlServer => "Use square brackets for table and column names (e.g. [table_name])",
        _ => "Quote identifiers appropriately for the database"
    };

    /// <summary>
    /// Reads allowlists out of the schema JSON when present; otherwise summarizes the raw payload.
    /// </summary>
    public static string FormatIdentifierAllowlist(string schemaJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            var tables = ReadStringArray(root, "allowedTables");
            var columns = ReadStringArray(root, "allowedColumns");

            if (tables.Count == 0 && root.TryGetProperty("table", out var tableEl)
                && tableEl.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var table = tableEl.GetString();
                if (!string.IsNullOrWhiteSpace(table))
                    tables.Add(table);
            }

            if (columns.Count == 0 && root.TryGetProperty("columns", out var cols)
                && cols.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var col in cols.EnumerateArray())
                {
                    if (col.ValueKind == System.Text.Json.JsonValueKind.Object
                        && col.TryGetProperty("name", out var nameEl)
                        && nameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            columns.Add(name);
                    }
                }
            }

            if (tables.Count == 0 && columns.Count == 0)
                return schemaJson;

            var sb = new StringBuilder();
            sb.AppendLine("- Allowed tables: " + (tables.Count == 0 ? "(none)" : string.Join(", ", tables)));
            sb.AppendLine("- Allowed columns: " + (columns.Count == 0 ? "(none)" : string.Join(", ", columns)));
            sb.Append("- Do NOT invent tables, columns, or JOINs to objects that are not listed");
            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return schemaJson;
        }
    }

    private static List<string> ReadStringArray(System.Text.Json.JsonElement root, string name)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(value);
            }
        }

        return list;
    }

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
}
