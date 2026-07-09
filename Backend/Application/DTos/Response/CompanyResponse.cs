namespace Application.Dtos.Response;

public sealed record CompanyResponse(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerName,
    int UserCount,
    List<CompanyRoleResponse> Roles,
    CompanySubscriptionResponse? CurrentSubscription);
