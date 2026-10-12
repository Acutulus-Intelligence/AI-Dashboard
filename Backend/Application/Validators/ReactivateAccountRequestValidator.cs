using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class ReactivateAccountRequestValidator : AbstractValidator<ReactivateAccountRequest>
{
    public ReactivateAccountRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.Code)
            .Matches(@"^\d{6}$")
            .WithMessage("Enter the 6-digit code from your authenticator app.")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.RecoveryCode)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.RecoveryCode));
    }
}
