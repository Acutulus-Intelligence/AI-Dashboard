using Application.DTos.Request;
using Domain.Charts;
using FluentValidation;

namespace Application.Validators;

public class SaveChartRequestValidator : AbstractValidator<SaveChartRequest>
{
    public SaveChartRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ChartType)
            .NotEmpty()
            .Must(ChartCatalog.IsKnownType)
            .WithMessage(_ => $"Chart type must be one of: {string.Join(", ", ChartCatalog.TypeIds)}.");

        RuleFor(x => x.XAxis)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.YAxis)
            .NotEmpty();

        RuleFor(x => x.SqlQuery)
            .MaximumLength(10_000)
            .When(x => !string.IsNullOrEmpty(x.SqlQuery));

        RuleFor(x => x.ConnectionId)
            .NotEmpty()
            .When(x => x.ConnectionId.HasValue);

        RuleFor(x => x.DatasetId)
            .NotEmpty()
            .When(x => x.DatasetId.HasValue);

        RuleFor(x => x)
            .Must(x => x.ConnectionId.HasValue != x.DatasetId.HasValue)
            .WithMessage("A chart must reference either a connection or a dataset, not both or neither.");

        RuleFor(x => x)
            .Must(x => x.SqlQuery is { Length: > 0 } || x.DataModel is not null)
            .WithMessage("A chart must include either a SQL query (connection) or a data model (dataset).");
    }
}
