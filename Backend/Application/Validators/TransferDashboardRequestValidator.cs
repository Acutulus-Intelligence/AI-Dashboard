using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class TransferDashboardRequestValidator : AbstractValidator<TransferDashboardRequest>
{
    public TransferDashboardRequestValidator()
    {
        RuleFor(x => x.NewOwnerId)
            .NotEmpty();

        RuleFor(x => x.CurrentPassword)
            .NotEmpty();
    }
}
