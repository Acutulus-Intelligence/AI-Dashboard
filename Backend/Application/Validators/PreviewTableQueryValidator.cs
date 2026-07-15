using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class PreviewTableQueryValidator : AbstractValidator<PreviewTableQuery>
{
    public const int MaxRows = 10;

    public PreviewTableQueryValidator()
    {
        RuleFor(x => x.Rows)
            .InclusiveBetween(1, MaxRows)
            .WithMessage($"Rows must be between 1 and {MaxRows}.");
    }
}
