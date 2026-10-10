using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class CreateChartFolderRequestValidator : AbstractValidator<CreateChartFolderRequest>
{
    public CreateChartFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public class UpdateChartFolderRequestValidator : AbstractValidator<UpdateChartFolderRequest>
{
    public UpdateChartFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
