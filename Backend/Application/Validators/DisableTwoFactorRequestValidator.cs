using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Enter the 6-digit code from your authenticator app.");
    }
}
