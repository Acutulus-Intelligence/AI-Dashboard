using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;

    public AdminService(IApplicationDbContext db, IPaymentService paymentService)
    {
        _db = db;
        _paymentService = paymentService;
    }

    public async Task<List<AdminSubscriptionPlanResponse>> GetAllPlansAsync(CancellationToken ct = default)
    {
        return await _db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.UserType)
            .ThenBy(p => p.MonthlyPrice)
            .Select(p => new AdminSubscriptionPlanResponse(
                p.Id,
                p.Name,
                p.Description,
                p.UserType,
                p.MonthlyPrice,
                p.YearlyPrice,
                p.MaxUsers,
                p.MaxDashboards,
                p.MaxAiQueriesPerMonth,
                p.IsActive,
                p.TrialDays,
                p.StripeProductId,
                p.StripeMonthlyPriceId,
                p.StripeYearlyPriceId
            ))
            .ToListAsync(ct);
    }

    public async Task<AdminSubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsArchived, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        return ToResponse(plan);
    }

    public async Task<AdminSubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default)
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
            IsActive = true,
            TrialDays = request.TrialDays
        };

        var productId = await _paymentService.CreateProductAsync(plan.Name, plan.Id.ToString(), ct);
        var monthlyPriceId = await _paymentService.CreatePriceAsync(productId, plan.MonthlyPrice, BillingPeriod.Monthly, ct);
        var yearlyPriceId = await _paymentService.CreatePriceAsync(productId, plan.YearlyPrice, BillingPeriod.Yearly, ct);

        plan.StripeProductId = productId;
        plan.StripeMonthlyPriceId = monthlyPriceId;
        plan.StripeYearlyPriceId = yearlyPriceId;

        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return ToResponse(plan);
    }

    public async Task<AdminSubscriptionPlanResponse> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsArchived, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        var monthlyChanged = plan.MonthlyPrice != request.MonthlyPrice;
        var yearlyChanged = plan.YearlyPrice != request.YearlyPrice;
        var nameChanged = plan.Name != request.Name;

        if (string.IsNullOrWhiteSpace(plan.StripeProductId))
        {
            plan.StripeProductId = await _paymentService.CreateProductAsync(request.Name, plan.Id.ToString(), ct);
        }
        else if (nameChanged)
        {
            await _paymentService.UpdateProductAsync(plan.StripeProductId, request.Name, ct);
        }

        var oldMonthlyPriceId = plan.StripeMonthlyPriceId;
        var oldYearlyPriceId = plan.StripeYearlyPriceId;

        if (monthlyChanged || string.IsNullOrWhiteSpace(plan.StripeMonthlyPriceId))
        {
            plan.StripeMonthlyPriceId = await _paymentService.CreatePriceAsync(plan.StripeProductId, request.MonthlyPrice, BillingPeriod.Monthly, ct);
        }

        if (yearlyChanged || string.IsNullOrWhiteSpace(plan.StripeYearlyPriceId))
        {
            plan.StripeYearlyPriceId = await _paymentService.CreatePriceAsync(plan.StripeProductId, request.YearlyPrice, BillingPeriod.Yearly, ct);
        }

        // Existing subscribers keep their current price for the ongoing period; the new
        // price only applies from the next renewal (proration "none" = no immediate charge).
        if (monthlyChanged && !string.IsNullOrWhiteSpace(plan.StripeMonthlyPriceId))
            await SwitchSubscriptionsAtRenewalAsync(planId, BillingPeriod.Monthly, plan.StripeMonthlyPriceId, request.MonthlyPrice, ct);

        if (yearlyChanged && !string.IsNullOrWhiteSpace(plan.StripeYearlyPriceId))
            await SwitchSubscriptionsAtRenewalAsync(planId, BillingPeriod.Yearly, plan.StripeYearlyPriceId, request.YearlyPrice, ct);

        // Old prices are only retired after every active subscription has moved to the new one.
        if (monthlyChanged && !string.IsNullOrWhiteSpace(oldMonthlyPriceId))
            await _paymentService.DeactivatePriceAsync(oldMonthlyPriceId, ct);

        if (yearlyChanged && !string.IsNullOrWhiteSpace(oldYearlyPriceId))
            await _paymentService.DeactivatePriceAsync(oldYearlyPriceId, ct);

        if (plan.IsActive && !request.IsActive)
            await _paymentService.DeactivateProductAsync(plan.StripeProductId, ct);
        else if (!plan.IsActive && request.IsActive)
            await _paymentService.ActivateProductAsync(plan.StripeProductId, ct);

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.UserType = request.UserType;
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.YearlyPrice = request.YearlyPrice;
        plan.MaxUsers = request.MaxUsers;
        plan.MaxDashboards = request.MaxDashboards;
        plan.MaxAiQueriesPerMonth = request.MaxAiQueriesPerMonth;
        plan.IsActive = request.IsActive;
        plan.TrialDays = request.TrialDays;

        await _db.SaveChangesAsync(ct);

        return ToResponse(plan);
    }

    private async Task SwitchSubscriptionsAtRenewalAsync(
        Guid planId,
        BillingPeriod billingPeriod,
        string newPriceId,
        decimal newPrice,
        CancellationToken ct)
    {
        var userSubscriptions = await _db.UserSubscriptions
            .Where(s => s.PlanId == planId &&
                s.BillingPeriod == billingPeriod &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active))
            .ToListAsync(ct);

        foreach (var subscription in userSubscriptions)
        {
            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.SwitchSubscriptionPriceAsync(
                    subscription.StripeSubscriptionId, newPriceId, "none", ct);

            subscription.NextPrice = newPrice;
            subscription.NextPriceEffectiveDate = subscription.EndDate ?? DateTime.UtcNow.AddMonths(1);
        }

        var companySubscriptions = await _db.CompanySubscriptions
            .Where(s => s.PlanId == planId &&
                s.BillingPeriod == billingPeriod &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active))
            .ToListAsync(ct);

        foreach (var subscription in companySubscriptions)
        {
            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.SwitchSubscriptionPriceAsync(
                    subscription.StripeSubscriptionId, newPriceId, "none", ct);

            subscription.NextPrice = newPrice;
            subscription.NextPriceEffectiveDate = subscription.EndDate ?? DateTime.UtcNow.AddMonths(1);
        }
    }

    public async Task DeletePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.IsActive)
            throw new InvalidOperationException("Deactivate the plan before removing it.");

        var userSubscriptions = await _db.UserSubscriptions
            .Where(s => s.PlanId == planId && s.StripeSubscriptionId != null)
            .ToListAsync(ct);

        foreach (var subscription in userSubscriptions)
        {
            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }

        var companySubscriptions = await _db.CompanySubscriptions
            .Where(s => s.PlanId == planId && s.StripeSubscriptionId != null)
            .ToListAsync(ct);

        foreach (var subscription in companySubscriptions)
        {
            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }

        plan.IsArchived = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePlanAsync(Guid sourcePlanId, Guid targetPlanId, CancellationToken ct = default)
    {
        if (sourcePlanId == targetPlanId)
            throw new InvalidOperationException("Source and target plans must be different.");

        var source = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == sourcePlanId, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (source.IsActive)
            throw new InvalidOperationException("Deactivate the plan before moving its subscriptions.");

        var target = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == targetPlanId, ct)
            ?? throw new KeyNotFoundException("Target plan not found.");

        if (!target.IsActive)
            throw new InvalidOperationException("Target plan must be active.");

        if (target.IsArchived)
            throw new InvalidOperationException("Target plan must not be archived.");

        if (target.UserType != source.UserType)
            throw new InvalidOperationException("Target plan must be the same type.");

        var userSubscriptions = await _db.UserSubscriptions
            .Where(s => s.PlanId == sourcePlanId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active) &&
                !s.CancelAtPeriodEnd)
            .ToListAsync(ct);

        foreach (var subscription in userSubscriptions)
        {
            var price = subscription.BillingPeriod == BillingPeriod.Monthly ? target.MonthlyPrice : target.YearlyPrice;
            var priceId = subscription.BillingPeriod == BillingPeriod.Monthly ? target.StripeMonthlyPriceId : target.StripeYearlyPriceId;

            if (subscription.StripeSubscriptionId is not null && !string.IsNullOrWhiteSpace(priceId))
                await _paymentService.SwitchSubscriptionPriceAsync(subscription.StripeSubscriptionId, priceId, ct: ct);

            subscription.PlanId = targetPlanId;
            subscription.Price = price;
            subscription.NextPrice = null;
            subscription.NextPriceEffectiveDate = null;
        }

        var companySubscriptions = await _db.CompanySubscriptions
            .Where(s => s.PlanId == sourcePlanId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active) &&
                !s.CancelAtPeriodEnd)
            .ToListAsync(ct);

        foreach (var subscription in companySubscriptions)
        {
            var price = subscription.BillingPeriod == BillingPeriod.Monthly ? target.MonthlyPrice : target.YearlyPrice;
            var priceId = subscription.BillingPeriod == BillingPeriod.Monthly ? target.StripeMonthlyPriceId : target.StripeYearlyPriceId;

            if (subscription.StripeSubscriptionId is not null && !string.IsNullOrWhiteSpace(priceId))
                await _paymentService.SwitchSubscriptionPriceAsync(subscription.StripeSubscriptionId, priceId, ct: ct);

            subscription.PlanId = targetPlanId;
            subscription.Price = price;
            subscription.MaxUsers = target.MaxUsers;
            subscription.NextPrice = null;
            subscription.NextPriceEffectiveDate = null;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static AdminSubscriptionPlanResponse ToResponse(SubscriptionPlan plan)
    {
        return new AdminSubscriptionPlanResponse(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.UserType,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.MaxUsers,
            plan.MaxDashboards,
            plan.MaxAiQueriesPerMonth,
            plan.IsActive,
            plan.TrialDays,
            plan.StripeProductId,
            plan.StripeMonthlyPriceId,
            plan.StripeYearlyPriceId
        );
    }
}
