namespace Application.DTos.Response;

public sealed record AdminStatsResponse(
    int TotalUsers,
    int IndividualSubscribedUsers,
    int CompanySubscribedUsers,
    int UsersWithoutSubscription);