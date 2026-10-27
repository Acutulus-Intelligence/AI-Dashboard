using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.Code)
            .Matches(@"^\d{6}$")
            .WithMessage("Enter the 6-digit code from your authenticator app.")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.RecoveryCode)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.RecoveryCode));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithMessage("Enter an authenticator code or a recovery code.")
            .WithName("Code");
    }
}
