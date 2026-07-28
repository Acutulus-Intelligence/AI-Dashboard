using Domain.Charts;

namespace Application.DTos.Response;

/// <summary>
/// Lets the frontend build its style panel from the same definitions the AI
/// prompt and request validation use.
/// </summary>
public sealed record ChartCatalogResponse(
    IReadOnlyList<ChartTypeSpec> Types,
    IReadOnlyList<ChartPaletteSpec> Palettes
);
