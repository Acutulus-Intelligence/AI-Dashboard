using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IAdminService
{
    Task<List<SubscriptionPlanResponse>> GetAllPlansAsync(CancellationToken ct = default);
    Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task<SubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default);
    Task<SubscriptionPlanResponse> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(Guid planId, CancellationToken ct = default);
}
