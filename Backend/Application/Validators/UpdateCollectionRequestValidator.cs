using Application.DTos.Request;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class UpdateCollectionRequestValidator : AbstractValidator<UpdateCollectionRequest>
{
    public UpdateCollectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.Visibility)
            .IsInEnum();

        RuleFor(x => x.AllowedRoleIds)
            .NotNull()
            .NotEmpty()
            .When(x => x.Visibility == CollectionVisibility.Roles)
            .WithMessage("Select at least one role to share this collection with.");
    }
}