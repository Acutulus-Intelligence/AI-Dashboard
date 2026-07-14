using Application.DTos.Request;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class SaveWidgetsRequestValidator : AbstractValidator<SaveWidgetsRequest>
{
    public SaveWidgetsRequestValidator()
    {
        RuleFor(x => x.Widgets)
            .NotNull();

        RuleForEach(x => x.Widgets).ChildRules(widget =>
        {
            widget.RuleFor(w => w.WidgetType).IsInEnum();
            widget.RuleFor(w => w.PositionX).GreaterThanOrEqualTo(0);
            widget.RuleFor(w => w.PositionY).GreaterThanOrEqualTo(0);
            widget.RuleFor(w => w.Width).InclusiveBetween(1, 12);
            widget.RuleFor(w => w.Height).InclusiveBetween(1, 20);

            widget.When(w => w.WidgetType == WidgetType.Chart, () =>
            {
                widget.RuleFor(w => w.SavedChartId)
                    .NotEmpty()
                    .WithMessage("Chart widgets must include a saved chart ID.");
            });

            widget.When(w => w.WidgetType == WidgetType.Text, () =>
            {
                widget.RuleFor(w => w.TextStyle)
                    .NotNull()
                    .IsInEnum();

                widget.RuleFor(w => w.HorizontalAlign)
                    .NotNull()
                    .IsInEnum();

                widget.RuleFor(w => w.VerticalAlign)
                    .NotNull()
                    .IsInEnum();

                widget.RuleFor(w => w.TextContent)
                    .MaximumLength(5000)
                    .When(w => w.TextContent != null);
            });
        });
    }
}
