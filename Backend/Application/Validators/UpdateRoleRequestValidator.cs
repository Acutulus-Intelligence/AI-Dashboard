using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .NoMarkupOrControlCharacters();
    }
}
