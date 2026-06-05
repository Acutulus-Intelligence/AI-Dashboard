using Domain.Enums;

namespace Application.DTos.Response;

public sealed record SubscriptionPlanResponse(
    Guid Id,
    string Name,
    string? Description,
    UserType UserType,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int? MaxUsers,
    int? MaxDashboards,
    int? MaxAiQueriesPerMonth,
    bool IsActive
);
