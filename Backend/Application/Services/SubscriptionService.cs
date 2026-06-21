using Application.Dtos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly ICompanyService _companyService;

    public SubscriptionService(
        IApplicationDbContext db,
        IPaymentService paymentService,
        ICompanyService companyService)
    {
        _db = db;
        _paymentService = paymentService;
        _companyService = companyService;
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

    public async Task<CheckoutResponse> CreateUserCheckoutSessionAsync(
        Guid userId, Guid planId, BillingPeriod period,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != user.UserType)
            throw new InvalidOperationException("This plan does not match your account type.");

        if (user.UserType != UserType.Individual)
            throw new InvalidOperationException("Individual checkout is for individual users only.");

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var existingSubscription = await _db.UserSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        var trialDays = CalculateTrialDays(existingSubscription?.TrialEndDate);

        var customerId = user.StripeCustomerId
            ?? await _paymentService.GetOrCreateCustomerAsync(user.Email!, user.Id, ct);

        var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
            customerId, user.Id, planId, plan.Name, price, period,
            trialDays, successUrl, cancelUrl, ct);

        return new CheckoutResponse(checkoutUrl);
    }

    public async Task<CheckoutResponse> CreateCompanyCheckoutSessionAsync(
        Guid companyId, Guid planId, BillingPeriod period, Guid actorId,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != actorId)
            throw new UnauthorizedAccessException("Only the company owner can manage subscriptions.");

        var owner = await _db.Users.FindAsync([actorId], ct)
            ?? throw new KeyNotFoundException("Owner not found.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != UserType.Company)
            throw new InvalidOperationException("This plan is for companies only.");

        var existingIndividualSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == actorId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        if (existingIndividualSub is not null)
        {
            existingIndividualSub.Status = SubscriptionStatus.Canceled;
            existingIndividualSub.EndDate = DateTime.UtcNow;

            if (existingIndividualSub.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionImmediatelyAsync(
                    existingIndividualSub.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var existingSubscription = await _db.CompanySubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        var trialDays = CalculateTrialDays(existingSubscription?.TrialEndDate);

        var customerId = owner.StripeCustomerId
            ?? await _paymentService.GetOrCreateCustomerAsync(owner.Email!, owner.Id, ct);

        var checkoutUrl = await _paymentService.CreateCompanyCheckoutSessionAsync(
            customerId, owner.Id, companyId, planId, plan.Name, price, period,
            trialDays, successUrl, cancelUrl, ct);

        return new CheckoutResponse(checkoutUrl);
    }

    public async Task<CheckoutResponse> UpgradeToCompanyAsync(
        Guid userId, string companyName, Guid planId, BillingPeriod period,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.UserType != UserType.Individual)
            throw new InvalidOperationException("Only individual users can upgrade to a company.");

        if (user.CompanyId is not null)
            throw new InvalidOperationException("You already belong to a company.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != UserType.Company)
            throw new InvalidOperationException("This plan is not a company plan.");

        var companyResponse = await _companyService.CreateAsync(userId, companyName, ct);

        var existingSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        if (existingSubscription is not null)
        {
            existingSubscription.Status = SubscriptionStatus.Canceled;
            existingSubscription.EndDate = DateTime.UtcNow;

            if (existingSubscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionImmediatelyAsync(
                    existingSubscription.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var trialDays = CalculateTrialDays(null);

        var customerId = user.StripeCustomerId
            ?? await _paymentService.GetOrCreateCustomerAsync(user.Email!, user.Id, ct);

        var checkoutUrl = await _paymentService.CreateCompanyCheckoutSessionAsync(
            customerId, user.Id, companyResponse.Id, planId, plan.Name, price, period,
            trialDays, successUrl, cancelUrl, ct);

        return new CheckoutResponse(checkoutUrl);
    }

    public async Task HandleStripeWebhookAsync(string body, string signature, CancellationToken ct = default)
    {
        var paymentEvent = await _paymentService.HandleWebhookAsync(body, signature);

        try
        {
            switch (paymentEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutCompletedAsync(paymentEvent, ct);
                    break;

                case "customer.subscription.created":
                    await HandleCheckoutCompletedAsync(paymentEvent, ct);
                    break;

                case "invoice.paid":
                    await HandleInvoicePaidAsync(paymentEvent, ct);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeletedAsync(paymentEvent, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[StripeWebhook] Error processing {paymentEvent.Type}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        if (await _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct))
            return true;

        return await _db.CompanySubscriptions
            .AnyAsync(s => s.Company.OwnerId == userId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);
    }

    public async Task<bool> CompanyHasActiveSubscriptionAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _db.CompanySubscriptions
            .AnyAsync(s => s.CompanyId == companyId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);
    }

    public async Task<UserSubscriptionResponse?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);

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
            subscription.Status,
            subscription.TrialEndDate
        );
    }

    public async Task<CompanySubscriptionResponse?> GetCurrentCompanySubscriptionAsync(Guid companyId, CancellationToken ct = default)
    {
        var subscription = await _db.CompanySubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);

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
            subscription.Status,
            subscription.TrialEndDate
        );
    }

    public async Task CancelUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct)
            ?? throw new InvalidOperationException("No active or trial subscription found.");

        if (subscription.Status == SubscriptionStatus.Trial)
        {
            subscription.Status = SubscriptionStatus.Canceled;

            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionImmediatelyAsync(subscription.StripeSubscriptionId, ct);
        }
        else
        {
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.EndDate = DateTime.UtcNow;

            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }

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
            .FirstOrDefaultAsync(s => s.CompanyId == companyId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct)
            ?? throw new InvalidOperationException("No active or trial subscription found.");

        if (subscription.Status == SubscriptionStatus.Trial)
        {
            subscription.Status = SubscriptionStatus.Canceled;

            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionImmediatelyAsync(subscription.StripeSubscriptionId, ct);
        }
        else
        {
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.EndDate = DateTime.UtcNow;

            if (subscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static int CalculateTrialDays(DateTime? trialEndDate)
    {
        if (trialEndDate is null)
            return 7;

        if (trialEndDate <= DateTime.UtcNow)
            return 0;

        var remaining = (int)(trialEndDate.Value - DateTime.UtcNow).TotalDays;
        return Math.Max(1, remaining);
    }

    private async Task HandleCheckoutCompletedAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        var userId = Guid.Parse(evt.Metadata["userId"]);
        var planId = Guid.Parse(evt.Metadata["planId"]);
        var billingPeriod = Enum.Parse<BillingPeriod>(evt.Metadata["billingPeriod"]);
        var isCompany = bool.Parse(evt.Metadata.GetValueOrDefault("isCompany", "false"));
        var trialDays = int.Parse(evt.Metadata.GetValueOrDefault("trialDays", "7"));

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is not null && evt.StripeCustomerId is not null)
        {
            user.StripeCustomerId = evt.StripeCustomerId;
        }

        var stripeSubscriptionId = evt.StripeSubscriptionId;
        if (stripeSubscriptionId is null)
        {
            Console.Error.WriteLine($"[StripeWebhook] No subscription ID in {evt.Type} event for customer {evt.StripeCustomerId}");
            return;
        }

        if (isCompany)
        {
            var companyId = Guid.Parse(evt.Metadata["companyId"]);
            var existing = await _db.CompanySubscriptions
                .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

            if (existing is not null)
            {
                existing.PlanId = planId;
                existing.BillingPeriod = billingPeriod;
                existing.StartDate = DateTime.UtcNow;
                existing.EndDate = DateTime.UtcNow.AddDays(trialDays);
                existing.Status = SubscriptionStatus.Trial;
                existing.StripeSubscriptionId = stripeSubscriptionId;
                existing.TrialEndDate ??= DateTime.UtcNow.AddDays(7);
            }
            else
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                _db.CompanySubscriptions.Add(new CompanySubscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    PlanId = planId,
                    Price = plan is not null
                        ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                        : 0,
                    BillingPeriod = billingPeriod,
                    MaxUsers = plan?.MaxUsers,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(trialDays),
                    Status = SubscriptionStatus.Trial,
                    StripeSubscriptionId = stripeSubscriptionId,
                    TrialEndDate = DateTime.UtcNow.AddDays(7)
                });
            }
        }
        else
        {
            var existing = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId, ct);

            if (existing is not null)
            {
                existing.PlanId = planId;
                existing.BillingPeriod = billingPeriod;
                existing.StartDate = DateTime.UtcNow;
                existing.EndDate = DateTime.UtcNow.AddDays(trialDays);
                existing.Status = SubscriptionStatus.Trial;
                existing.StripeSubscriptionId = stripeSubscriptionId;
                existing.TrialEndDate ??= DateTime.UtcNow.AddDays(7);
            }
            else
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                _db.UserSubscriptions.Add(new UserSubscription
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlanId = planId,
                    Price = plan is not null
                        ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                        : 0,
                    BillingPeriod = billingPeriod,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(trialDays),
                    Status = SubscriptionStatus.Trial,
                    StripeSubscriptionId = stripeSubscriptionId,
                    TrialEndDate = DateTime.UtcNow.AddDays(7)
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoicePaidAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.StripeSubscriptionId is null)
            return;

        var userSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (userSub is not null)
        {
            if (userSub.Status == SubscriptionStatus.Trial)
                userSub.Status = SubscriptionStatus.Active;

            userSub.EndDate = userSub.BillingPeriod == BillingPeriod.Monthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1);

            await _db.SaveChangesAsync(ct);
            return;
        }

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (companySub is not null)
        {
            if (companySub.Status == SubscriptionStatus.Trial)
                companySub.Status = SubscriptionStatus.Active;

            companySub.EndDate = companySub.BillingPeriod == BillingPeriod.Monthly
                ? DateTime.UtcNow.AddMonths(1)
                : DateTime.UtcNow.AddYears(1);

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task HandleSubscriptionDeletedAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.StripeSubscriptionId is null)
            return;

        var userSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (userSub is not null)
        {
            userSub.Status = SubscriptionStatus.Canceled;
            userSub.EndDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (companySub is not null)
        {
            companySub.Status = SubscriptionStatus.Canceled;
            companySub.EndDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
