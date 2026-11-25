using Application.DTos.Request;
using Domain.Charts;
using FluentValidation;

namespace Application.Validators;

public class UpdateChartRequestValidator : AbstractValidator<UpdateChartRequest>
{
    public UpdateChartRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ChartType)
            .NotEmpty()
            .Must(ChartCatalog.IsKnownType)
            .WithMessage(_ => $"Chart type must be one of: {string.Join(", ", ChartCatalog.TypeIds)}.");

        RuleFor(x => x.XAxis)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.YAxis)
            .NotEmpty();

        RuleFor(x => x.Aggregation)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.SqlQuery)
            .MaximumLength(10_000)
            .When(x => !string.IsNullOrEmpty(x.SqlQuery));

        RuleFor(x => x)
            .Must(x => x.SqlQuery is { Length: > 0 } || x.DataModel is not null)
            .WithMessage("A chart must include either a SQL query (connection) or a data model (dataset).");

        RuleFor(x => x.StyleConfig!.Variant)
            .Must((request, variant) =>
                ChartCatalog.Find(request.ChartType)?.Variants
                    .Any(v => v.Id.Equals(variant, StringComparison.OrdinalIgnoreCase)) ?? false)
            .When(x => x.StyleConfig?.Variant is not null)
            .WithMessage("Unknown chart variant for this chart type.");

        RuleFor(x => x.StyleConfig!.Palette)
            .Must(ChartCatalog.IsKnownPalette)
            .When(x => x.StyleConfig?.Palette is not null)
            .WithMessage("Unknown palette.");
    }
}
