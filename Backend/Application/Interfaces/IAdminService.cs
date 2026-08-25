using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IAdminService
{
    Task<List<AdminSubscriptionPlanResponse>> GetAllPlansAsync(CancellationToken ct = default);
    Task<AdminSubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task<AdminSubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default);
    Task<AdminSubscriptionPlanResponse> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(Guid planId, CancellationToken ct = default);
    Task MovePlanAsync(Guid sourcePlanId, Guid targetPlanId, CancellationToken ct = default);
}
