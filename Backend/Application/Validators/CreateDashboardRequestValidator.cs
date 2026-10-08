using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class CreateDashboardRequestValidator : AbstractValidator<CreateDashboardRequest>
{
    public CreateDashboardRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
