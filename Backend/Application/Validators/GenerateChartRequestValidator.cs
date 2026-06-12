using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class GenerateChartRequestValidator : AbstractValidator<GenerateChartRequest>
{
    public GenerateChartRequestValidator()
    {
        RuleFor(x => x.ConnectionId)
            .NotEmpty();

        RuleFor(x => x.TableName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => m is "prompt" or "prefab" or "auto")
            .WithMessage("Mode must be 'prompt', 'prefab', or 'auto'.");

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
                .WithMessage("PrefabChartType is required when mode is 'prefab'.");
        });
    }
}
