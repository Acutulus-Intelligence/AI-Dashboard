using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class SetupTwoFactorRequestValidator : AbstractValidator<SetupTwoFactorRequest>
{
    public SetupTwoFactorRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
