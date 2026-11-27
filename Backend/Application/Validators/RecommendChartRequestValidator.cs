using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class RecommendChartRequestValidator : AbstractValidator<RecommendChartRequest>
{
    public RecommendChartRequestValidator()
    {
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.TableName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
