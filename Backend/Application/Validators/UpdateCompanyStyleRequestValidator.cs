using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class UpdateCompanyStyleRequestValidator : AbstractValidator<UpdateCompanyStyleRequest>
{
    public UpdateCompanyStyleRequestValidator()
    {
        RuleFor(x => x.Colors)
            .NotNull()
            .Must(c => c.Count > 0)
            .WithMessage("At least one colour is required.")
            .Must(c => c.Count <= 24)
            .WithMessage("A maximum of 24 colours is allowed.");
    }
}
