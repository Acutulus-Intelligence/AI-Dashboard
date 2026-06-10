using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;

    public SubscriptionService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SubscriptionPlanResponse>> GetPlansAsync(UserType? userType = null, CancellationToken ct = default)
    {
        var query = _db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (userType.HasValue)
            query = query.Where(p => p.UserType == userType.Value);

        var plans = await query
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

        return plans;
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

    public async Task<UserSubscriptionResponse> SubscribeUserAsync(Guid userId, Guid planId, BillingPeriod period, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != user.UserType)
            throw new InvalidOperationException("This plan does not match your account type.");

        if (user.UserType != UserType.Individual)
            throw new InvalidOperationException("Individual subscription endpoint is for individual users only. Company users should subscribe via the company endpoint.");

        var existingSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);

        if (existingSubscription is not null)
        {
            existingSubscription.Status = SubscriptionStatus.Canceled;
            existingSubscription.EndDate = DateTime.UtcNow;
        }

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = planId,
            Price = price,
            BillingPeriod = period,
            StartDate = DateTime.UtcNow,
            EndDate = period == BillingPeriod.Monthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1),
            Status = SubscriptionStatus.Active
        };

        _db.UserSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return new UserSubscriptionResponse(
            subscription.Id,
            subscription.PlanId,
            plan.Name,
            subscription.Price,
            subscription.BillingPeriod,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status
        );
    }

    public async Task<CompanySubscriptionResponse> SubscribeCompanyAsync(Guid companyId, Guid planId, BillingPeriod period, Guid actorId, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != actorId)
            throw new UnauthorizedAccessException("Only the company owner can manage subscriptions.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != UserType.Company)
            throw new InvalidOperationException("This plan is for individuals only.");

        var existingSubscription = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Status == SubscriptionStatus.Active, ct);

        if (existingSubscription is not null)
        {
            existingSubscription.Status = SubscriptionStatus.Canceled;
            existingSubscription.EndDate = DateTime.UtcNow;
        }

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanId = planId,
            Price = price,
            BillingPeriod = period,
            MaxUsers = plan.MaxUsers,
            StartDate = DateTime.UtcNow,
            EndDate = period == BillingPeriod.Monthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1),
            Status = SubscriptionStatus.Active
        };

        _db.CompanySubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return new CompanySubscriptionResponse(
            subscription.Id,
            subscription.PlanId,
            plan.Name,
            subscription.Price,
            subscription.BillingPeriod,
            subscription.MaxUsers,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status
        );
    }

    public async Task CancelUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct)
            ?? throw new InvalidOperationException("No active subscription found.");

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.EndDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelCompanySubscriptionAsync(Guid companyId, Guid actorId, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != actorId)
            throw new UnauthorizedAccessException("Only the company owner can manage subscriptions.");

        var subscription = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Status == SubscriptionStatus.Active, ct)
            ?? throw new InvalidOperationException("No active subscription found.");

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.EndDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserSubscriptionResponse?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);

        if (subscription is null)
            return null;

        return new UserSubscriptionResponse(
            subscription.Id,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Price,
            subscription.BillingPeriod,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status
        );
    }

    public async Task<CompanySubscriptionResponse?> GetCurrentCompanySubscriptionAsync(Guid companyId, CancellationToken ct = default)
    {
        var subscription = await _db.CompanySubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Status == SubscriptionStatus.Active, ct);

        if (subscription is null)
            return null;

        return new CompanySubscriptionResponse(
            subscription.Id,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Price,
            subscription.BillingPeriod,
            subscription.MaxUsers,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status
        );
    }
}
