using Application.DTos.Request;
using Domain.Charts;
using FluentValidation;

namespace Application.Validators;

public class SaveChartRequestValidator : AbstractValidator<SaveChartRequest>
{
    public SaveChartRequestValidator()
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

        RuleFor(x => x.SqlQuery)
            .NotEmpty()
            .MaximumLength(10_000);

        RuleFor(x => x.ConnectionId)
            .NotEmpty()
            .When(x => x.ConnectionId.HasValue);
    }
}
