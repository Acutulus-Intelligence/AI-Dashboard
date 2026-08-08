using Application.DTos.Request;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class UpdateConnectionRequestValidator : AbstractValidator<UpdateConnectionRequest>
{
    public UpdateConnectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DbProvider)
            .IsInEnum();

        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Visibility)
            .IsInEnum();

        RuleFor(x => x.AllowedRoleIds)
            .NotNull()
            .NotEmpty()
            .When(x => x.Visibility == ConnectionVisibility.Roles)
            .WithMessage("Select at least one role to share this connection with.");
    }
}