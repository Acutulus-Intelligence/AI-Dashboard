using Domain.Enums;

namespace Application.DTos.Request;

public sealed record CreateCollectionRequest(
    string Name,
    string? Description,
    CollectionVisibility Visibility = CollectionVisibility.Company,
    List<Guid>? AllowedRoleIds = null
);

public sealed record UpdateCollectionRequest(
    string Name,
    string? Description,
    CollectionVisibility Visibility = CollectionVisibility.Company,
    List<Guid>? AllowedRoleIds = null
);

public sealed record GenerateCollectionChartRequest(
    string? Prompt,
    string? PrefabChartType,
    string Mode
);