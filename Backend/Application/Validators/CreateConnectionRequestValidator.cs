using Application.DTos.Request;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class CreateConnectionRequestValidator : AbstractValidator<CreateConnectionRequest>
{
    public CreateConnectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DbProvider)
            .IsInEnum();

        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535);

        RuleFor(x => x.Database)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(500);
    }
}
