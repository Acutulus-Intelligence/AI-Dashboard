using Domain.Enums;

namespace Application.DTos.Request;

public sealed record CreateSubscriptionPlanRequest(
    string Name,
    string? Description,
    UserType UserType,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int? MaxUsers,
    int? MaxDashboards,
    int? MaxAiQueriesPerMonth
);
