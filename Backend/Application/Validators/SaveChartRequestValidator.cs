using Application.DTos.Request;
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
            .MaximumLength(50);

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
