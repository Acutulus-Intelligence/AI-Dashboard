using Application.DTos.Request;
using Domain.Charts;
using FluentValidation;

namespace Application.Validators;

public class GenerateCollectionChartRequestValidator : AbstractValidator<GenerateCollectionChartRequest>
{
    public GenerateCollectionChartRequestValidator()
    {
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => m is "prompt" or "prefab" or "auto")
            .WithMessage("Mode must be 'prompt', 'prefab', or 'auto'.");

        RuleFor(x => x.Prompt).MaximumLength(2000);

        When(x => x.Mode == "prompt", () =>
        {
            RuleFor(x => x.Prompt)
                .NotEmpty()
                .WithMessage("Prompt is required when mode is 'prompt'.");
        });

        When(x => x.Mode == "prefab", () =>
        {
            RuleFor(x => x.PrefabChartType)
                .NotEmpty()
                .WithMessage("PrefabChartType is required when mode is 'prefab'.")
                .Must(ChartCatalog.IsKnownType)
                .WithMessage(_ => $"PrefabChartType must be one of: {string.Join(", ", ChartCatalog.TypeIds)}.");
        });
    }
}
