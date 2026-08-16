using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public class CreateSubscriptionPlanRequestValidator : AbstractValidator<CreateSubscriptionPlanRequest>
{
    public CreateSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.UserType)
            .IsInEnum();

        RuleFor(x => x.MonthlyPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.YearlyPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.MaxUsers)
            .GreaterThan(0)
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.MaxDashboards)
            .GreaterThan(0)
            .When(x => x.MaxDashboards.HasValue);

        RuleFor(x => x.MaxAiQueriesPerMonth)
            .GreaterThan(0)
            .When(x => x.MaxAiQueriesPerMonth.HasValue);

        RuleFor(x => x.TrialDays)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(365)
            .When(x => x.TrialDays.HasValue);
    }
}
