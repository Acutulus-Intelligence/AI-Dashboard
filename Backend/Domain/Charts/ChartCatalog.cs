using System.Text.Json.Serialization;

namespace Domain.Charts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChartParamKind
{
    [JsonStringEnumMemberName("boolean")]
    Boolean,
    [JsonStringEnumMemberName("number")]
    Number,
    [JsonStringEnumMemberName("select")]
    Select
}

public sealed record ChartParamOption(string Value, string Label);

public sealed record ChartParamSpec
{
    public required ChartParamKind Kind { get; init; }
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required object Default { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }
    public IReadOnlyList<ChartParamOption>? Options { get; init; }
    public string? Help { get; init; }
}

public sealed record ChartVariantSpec(string Id, string Label, string Description);

public sealed record ChartTypeSpec
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<ChartVariantSpec> Variants { get; init; }
    public required IReadOnlyList<ChartParamSpec> Params { get; init; }
}

public sealed record ChartPaletteSpec(string Id, string Label);

/// <summary>
/// The single source of truth for which chart types, variants and parameters
/// exist. The AI prompt, request validation and the frontend style panel are all
/// generated from this, so adding a chart type only requires editing this file
/// and the matching frontend descriptor.
/// </summary>
public static class ChartCatalog
{
    private static ChartParamSpec Toggle(string key, string label, bool @default, string? help = null) =>
        new() { Kind = ChartParamKind.Boolean, Key = key, Label = label, Default = @default, Help = help };

    private static ChartParamSpec Number(
        string key, string label, double @default, double min, double max, double step, string? help = null) =>
        new()
        {
            Kind = ChartParamKind.Number,
            Key = key,
            Label = label,
            Default = @default,
            Min = min,
            Max = max,
            Step = step,
            Help = help
        };

    private static ChartParamSpec Select(
        string key, string label, string @default, params (string Value, string Label)[] options) =>
        new()
        {
            Kind = ChartParamKind.Select,
            Key = key,
            Label = label,
            Default = @default,
            Options = [.. options.Select(o => new ChartParamOption(o.Value, o.Label))]
        };

    private static readonly ChartParamSpec ShowGrid = Toggle("showGrid", "Grid lines", true);
    private static readonly ChartParamSpec ShowLegend = Toggle("showLegend", "Legend", false);
    private static readonly ChartParamSpec ShowTooltip = Toggle("showTooltip", "Tooltip", true);
    private static readonly ChartParamSpec ShowXAxis = Toggle("showXAxis", "X axis", true);
    private static readonly ChartParamSpec ShowYAxis = Toggle("showYAxis", "Y axis", true);
    private static readonly ChartParamSpec ShowLabels = Toggle("showLabels", "Value labels", false);
    private static readonly ChartParamSpec StrokeWidth = Number("strokeWidth", "Line thickness", 2, 1, 6, 1);
    private static readonly ChartParamSpec FillOpacity = Number("fillOpacity", "Fill opacity", 0.25, 0, 1, 0.05);
    private static readonly ChartParamSpec CornerRadius = Number("cornerRadius", "Corner radius", 4, 0, 16, 1);

    private static readonly ChartParamSpec Curve = Select(
        "curve", "Curve", "monotone",
        ("monotone", "Smooth"), ("linear", "Straight"), ("step", "Stepped"));

    public static IReadOnlyList<ChartPaletteSpec> Palettes { get; } =
    [
        new("default", "Default"),
        new("cool", "Cool"),
        new("warm", "Warm"),
        new("contrast", "High contrast"),
        new("mono", "Monochrome")
    ];

    public static IReadOnlyList<ChartTypeSpec> Types { get; } =
    [
        new()
        {
            Id = "bar",
            Label = "Bar",
            Description = "Compares a value across categories.",
            Variants =
            [
                new("default", "Grouped", "Series side by side."),
                new("stacked", "Stacked", "Series stacked into one bar."),
                new("horizontal", "Horizontal", "Bars run left to right.")
            ],
            Params =
            [
                CornerRadius, ShowGrid, ShowXAxis, ShowYAxis, ShowTooltip, ShowLegend, ShowLabels
            ]
        },
        new()
        {
            Id = "line",
            Label = "Line",
            Description = "Shows how values change over a sequence.",
            Variants =
            [
                new("default", "Line", "A plain line per series."),
                new("dashed", "Dashed", "Dashed strokes."),
                new("step", "Step", "Values hold until the next point.")
            ],
            Params =
            [
                Curve,
                StrokeWidth,
                Number("dotSize", "Point size", 0, 0, 8, 1, "Zero hides the points."),
                ShowGrid, ShowXAxis, ShowYAxis, ShowTooltip, ShowLegend, ShowLabels
            ]
        },
        new()
        {
            Id = "area",
            Label = "Area",
            Description = "A line chart with the area below it filled.",
            Variants =
            [
                new("default", "Overlapping", "Series drawn on top of each other."),
                new("stacked", "Stacked", "Series accumulate."),
                new("gradient", "Gradient", "Fill fades towards the baseline.")
            ],
            Params =
            [
                Curve, StrokeWidth, FillOpacity, ShowGrid, ShowXAxis, ShowYAxis, ShowTooltip, ShowLegend
            ]
        },
        new()
        {
            Id = "pie",
            Label = "Pie",
            Description = "Shows each category as a share of the whole.",
            Variants =
            [
                new("default", "Pie", "A solid pie."),
                new("donut", "Donut", "Hollow centre."),
                new("total", "Donut with total", "Donut showing the sum in the middle.")
            ],
            Params =
            [
                Number("innerRadius", "Hole size", 0, 0, 80, 5, "Percent of the radius left empty in the middle."),
                Number("padAngle", "Slice gap", 0, 0, 8, 1),
                ShowLabels, ShowTooltip, ShowLegend
            ]
        },
        new()
        {
            Id = "radar",
            Label = "Radar",
            Description = "Compares several measures around a circle.",
            Variants =
            [
                new("default", "Filled", "Filled area per series."),
                new("lines", "Outline", "Stroke only, no fill."),
                new("dots", "Dots", "Filled with a point at each axis.")
            ],
            Params =
            [
                StrokeWidth, FillOpacity, ShowGrid, Toggle("showDots", "Points", false), ShowTooltip, ShowLegend
            ]
        },
        new()
        {
            Id = "radial",
            Label = "Radial",
            Description = "Draws each category as an arc, good for progress-style values.",
            Variants =
            [
                new("default", "Rings", "One ring per category."),
                new("labelled", "With labels", "Category names drawn on the rings."),
                new("stacked", "Stacked", "Categories share one ring.")
            ],
            Params =
            [
                Number("barSize", "Ring thickness", 14, 4, 40, 2),
                Number("startAngle", "Start angle", 90, -180, 360, 15),
                Number("endAngle", "End angle", -270, -360, 360, 15),
                ShowGrid, ShowLabels, ShowTooltip
            ]
        },
        new()
        {
            Id = "scatter",
            Label = "Scatter",
            Description = "Plots individual points to reveal spread and outliers.",
            Variants =
            [
                new("default", "Points", "One point per row."),
                new("bubble", "Bubble", "Point size scales with the value.")
            ],
            Params =
            [
                Number("pointSize", "Point size", 60, 20, 240, 10),
                Number("pointOpacity", "Point opacity", 0.8, 0.1, 1, 0.05),
                Select("pointShape", "Point shape", "circle",
                    ("circle", "Circle"), ("square", "Square"), ("triangle", "Triangle"),
                    ("diamond", "Diamond"), ("cross", "Cross"), ("star", "Star")),
                ShowGrid, ShowXAxis, ShowYAxis, ShowTooltip, ShowLegend
            ]
        },
        new()
        {
            Id = "table",
            Label = "Table",
            Description = "Shows the query result as rows and columns.",
            Variants =
            [
                new("raw", "Query result", "Every column the query returned."),
                new("summary", "Summary", "Only the chart axes, pivoted by label.")
            ],
            Params =
            [
                Toggle("striped", "Striped rows", false),
                Toggle("compact", "Compact rows", false),
                Toggle("stickyHeader", "Sticky header", true),
                Toggle("alignNumbers", "Right-align numbers", true),
                Number("maxRows", "Row limit", 100, 5, 500, 5)
            ]
        }
    ];

    private static readonly Dictionary<string, ChartTypeSpec> ByIdLookup =
        Types.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

    public static ChartTypeSpec? Find(string? chartType) =>
        chartType is not null && ByIdLookup.TryGetValue(chartType, out var spec) ? spec : null;

    public static bool IsKnownType(string? chartType) => Find(chartType) is not null;

    public static bool IsKnownPalette(string? palette) =>
        palette is not null && Palettes.Any(p => p.Id.Equals(palette, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> TypeIds { get; } = [.. Types.Select(t => t.Id)];
}
