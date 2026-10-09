using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class RenameDashboardRequestValidator : AbstractValidator<RenameDashboardRequest>
{
    public RenameDashboardRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
