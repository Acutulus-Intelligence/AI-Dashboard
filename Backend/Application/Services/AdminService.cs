using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;

    public AdminService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SubscriptionPlanResponse>> GetAllPlansAsync(CancellationToken ct = default)
    {
        return await _db.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.UserType)
            .ThenBy(p => p.MonthlyPrice)
            .Select(p => new SubscriptionPlanResponse(
                p.Id,
                p.Name,
                p.Description,
                p.UserType,
                p.MonthlyPrice,
                p.YearlyPrice,
                p.MaxUsers,
                p.MaxDashboards,
                p.MaxAiQueriesPerMonth,
                p.IsActive
            ))
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        return new SubscriptionPlanResponse(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.UserType,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.MaxUsers,
            plan.MaxDashboards,
            plan.MaxAiQueriesPerMonth,
            plan.IsActive
        );
    }

    public async Task<SubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            UserType = request.UserType,
            MonthlyPrice = request.MonthlyPrice,
            YearlyPrice = request.YearlyPrice,
            MaxUsers = request.MaxUsers,
            MaxDashboards = request.MaxDashboards,
            MaxAiQueriesPerMonth = request.MaxAiQueriesPerMonth,
            IsActive = true
        };

        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return new SubscriptionPlanResponse(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.UserType,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.MaxUsers,
            plan.MaxDashboards,
            plan.MaxAiQueriesPerMonth,
            plan.IsActive
        );
    }

    public async Task<SubscriptionPlanResponse> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.UserType = request.UserType;
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.YearlyPrice = request.YearlyPrice;
        plan.MaxUsers = request.MaxUsers;
        plan.MaxDashboards = request.MaxDashboards;
        plan.MaxAiQueriesPerMonth = request.MaxAiQueriesPerMonth;
        plan.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);

        return new SubscriptionPlanResponse(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.UserType,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.MaxUsers,
            plan.MaxDashboards,
            plan.MaxAiQueriesPerMonth,
            plan.IsActive
        );
    }

    public async Task DeletePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        plan.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }
}
