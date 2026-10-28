using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class AcceptEmailInvitationRequestValidator : AbstractValidator<AcceptEmailInvitationRequest>
{
    public AcceptEmailInvitationRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .StrongPassword();
    }
}
