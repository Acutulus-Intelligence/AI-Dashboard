namespace Application.DTos.Response;

public sealed record SubscriptionPlanResponse(
    Guid Id,
    string Name,
    string Description,
    Domain.Enums.UserType UserType,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int? MaxUsers,
    int? MaxDashboards,
    int? MaxAiQueriesPerMonth,
    bool IsActive);
