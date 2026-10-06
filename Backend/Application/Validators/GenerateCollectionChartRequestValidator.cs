using Application.DTos.Request;
using Domain.Charts;
using FluentValidation;

namespace Application.Validators;

public class GenerateCollectionChartRequestValidator : AbstractValidator<GenerateCollectionChartRequest>
{
    public GenerateCollectionChartRequestValidator()
    {
        When(x => x.Mode == "prefab", () =>
        {
            RuleFor(x => x.PrefabChartType)
                .Must(ChartCatalog.IsKnownType)
                .WithMessage(_ => $"PrefabChartType must be one of: {string.Join(", ", ChartCatalog.TypeIds)}.");
        });
    }
}
