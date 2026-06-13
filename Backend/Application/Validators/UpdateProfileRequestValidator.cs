using Application.Dtos.Request;
using FluentValidation;

namespace Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!)
                .EmailAddress();
        });
    }
}
