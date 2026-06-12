using Application.Dtos.Request;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.UserType)
            .IsInEnum();

        When(x => x.UserType == UserType.Company, () =>
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty()
                .WithMessage("Company name is required when registering as a company.")
                .When(x => string.IsNullOrEmpty(x.InviteToken));

            RuleFor(x => x.InviteToken)
                .NotEmpty()
                .WithMessage("Invite token is required when joining an existing company.")
                .When(x => string.IsNullOrEmpty(x.CompanyName));
        });
    }
}
