using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class LookupInvitationRequestValidator : AbstractValidator<LookupInvitationRequest>
{
    public LookupInvitationRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(300);
    }
}
