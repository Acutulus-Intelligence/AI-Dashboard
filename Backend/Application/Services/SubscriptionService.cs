using System.Security.Cryptography;
using System.Text;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private const int DefaultTrialDays = 7;

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
            .Where(p => p.IsActive && !p.IsArchived);

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
                p.IsActive,
                p.TrialDays
            ))
            .ToListAsync(ct);

        return plans;
    }

    public async Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive && !p.IsArchived, ct)
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
            plan.IsActive,
            plan.TrialDays
        );
    }

    public async Task<CheckoutResponse> CreateUserCheckoutSessionAsync(
        Guid userId, Guid planId, BillingPeriod period,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.CompanyId is not null)
            throw new InvalidOperationException("You must leave your company before switching to an individual plan.");

        var hasActive = await _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        if (hasActive)
            throw new InvalidOperationException("You already have an active subscription.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var existingSubscription = await _db.UserSubscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        var userEmail = user.Email ?? throw new InvalidOperationException("User email is required for payment processing.");
        var trialDays = await ResolveTrialDaysAsync(userId, userEmail, plan.TrialDays ?? DefaultTrialDays, existingSubscription?.TrialEndDate, ct);
        var customerId = user.StripeCustomerId is not null
            ? await _paymentService.EnsureCustomerExistsAsync(user.StripeCustomerId, userEmail, user.Id, ct)
            : await _paymentService.GetOrCreateCustomerAsync(userEmail, user.Id, ct);

        if (customerId != user.StripeCustomerId)
        {
            user.StripeCustomerId = customerId;
            await _db.SaveChangesAsync(ct);
        }

        var priceId = period == BillingPeriod.Monthly ? plan.StripeMonthlyPriceId : plan.StripeYearlyPriceId;

        return await _paymentService.CreateCheckoutSessionAsync(
            customerId, user.Id, planId, plan.Name, price, priceId, period,
            trialDays, successUrl, cancelUrl, ct);
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

        var companyHasActive = await _db.CompanySubscriptions
            .AnyAsync(s => s.CompanyId == companyId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        if (companyHasActive)
            throw new InvalidOperationException("This company already has an active subscription.");

        var owner = await _db.Users.FindAsync([actorId], ct)
            ?? throw new KeyNotFoundException("Owner not found.");

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        var existingIndividualSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == actorId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        var now = DateTime.UtcNow;

        if (existingIndividualSub is not null)
        {
            // Upgrade switches plans now; the unused individual period becomes a Stripe
            // customer credit that is applied to the new company subscription's first invoice.
            existingIndividualSub.Status = SubscriptionStatus.Canceled;
            existingIndividualSub.EndDate = now;
            existingIndividualSub.CancelAtPeriodEnd = false;

            if (existingIndividualSub.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionWithProrationAsync(
                    existingIndividualSub.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var existingSubscription = await _db.CompanySubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        var existingUserSubscription = await _db.UserSubscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(s => s.UserId == actorId, ct);

        var ownerEmail = owner.Email ?? throw new InvalidOperationException("Owner email is required for payment processing.");
        var trialDays = await ResolveTrialDaysAsync(
            actorId,
            ownerEmail,
            plan.TrialDays ?? DefaultTrialDays,
            existingSubscription?.TrialEndDate ?? existingUserSubscription?.TrialEndDate,
            ct);
        var customerId = owner.StripeCustomerId is not null
            ? await _paymentService.EnsureCustomerExistsAsync(owner.StripeCustomerId, ownerEmail, owner.Id, ct)
            : await _paymentService.GetOrCreateCustomerAsync(ownerEmail, owner.Id, ct);

        if (customerId != owner.StripeCustomerId)
        {
            owner.StripeCustomerId = customerId;
            await _db.SaveChangesAsync(ct);
        }

        var priceId = period == BillingPeriod.Monthly ? plan.StripeMonthlyPriceId : plan.StripeYearlyPriceId;

        return await _paymentService.CreateCompanyCheckoutSessionAsync(
            customerId, owner.Id, companyId, planId, plan.Name, price, priceId, period,
            trialDays, successUrl, cancelUrl, ct);
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
        {
            var companyHasActive = await _db.CompanySubscriptions
                .AnyAsync(s => s.CompanyId == user.CompanyId &&
                    (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

            if (companyHasActive)
                throw new InvalidOperationException("Your company already has an active subscription.");

            return await CreateCompanyCheckoutSessionAsync(
                user.CompanyId.Value, planId, period, userId, successUrl, cancelUrl, ct);
        }

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Subscription plan not found.");

        if (plan.UserType != UserType.Company)
            throw new InvalidOperationException("This plan is not a company plan.");

        var companyResponse = await _companyService.CreateAsync(userId, companyName, ct);

        var existingSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct);

        var historicalSubscription = await _db.UserSubscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        var now = DateTime.UtcNow;

        if (existingSubscription is not null)
        {
            // Upgrade switches plans now; the unused individual period becomes a Stripe
            // customer credit that is applied to the new company subscription's first invoice.
            existingSubscription.Status = SubscriptionStatus.Canceled;
            existingSubscription.EndDate = now;
            existingSubscription.CancelAtPeriodEnd = false;

            if (existingSubscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionWithProrationAsync(
                    existingSubscription.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);

        var price = period == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;

        var upgradeEmail = user.Email ?? throw new InvalidOperationException("User email is required for payment processing.");
        var trialDays = await ResolveTrialDaysAsync(
            userId,
            upgradeEmail,
            plan.TrialDays ?? DefaultTrialDays,
            existingSubscription?.TrialEndDate ?? historicalSubscription?.TrialEndDate,
            ct);
        var customerId = user.StripeCustomerId is not null
            ? await _paymentService.EnsureCustomerExistsAsync(user.StripeCustomerId, upgradeEmail, user.Id, ct)
            : await _paymentService.GetOrCreateCustomerAsync(upgradeEmail, user.Id, ct);

        if (customerId != user.StripeCustomerId)
        {
            user.StripeCustomerId = customerId;
            await _db.SaveChangesAsync(ct);
        }

        var priceId = period == BillingPeriod.Monthly ? plan.StripeMonthlyPriceId : plan.StripeYearlyPriceId;

        return await _paymentService.CreateCompanyCheckoutSessionAsync(
            customerId, user.Id, companyResponse.Id, planId, plan.Name, price, priceId, period,
            trialDays, successUrl, cancelUrl, ct);
    }

    public async Task ConfirmCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var evt = await _paymentService.RetrieveCheckoutSessionAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Checkout session not found or payment not completed.");

        await HandleCheckoutCompletedAsync(evt, ct);
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

                case "customer.subscription.updated":
                    await HandleSubscriptionUpdatedAsync(paymentEvent, ct);
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
            throw;
        }
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Fallback: an Active row whose paid period has ended counts as inactive if the
        // customer.subscription.deleted webhook was missed (cancel-at-period-end relies on it).
        var userSubActive = await _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId &&
                ((s.Status == SubscriptionStatus.Trial && (s.EndDate == null || s.EndDate > now)) ||
                 (s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now))), ct);

        if (userSubActive)
            return true;

        var companyId = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync(ct);

        return companyId.HasValue && await _db.CompanySubscriptions
            .AnyAsync(s => s.CompanyId == companyId.Value &&
                ((s.Status == SubscriptionStatus.Trial && (s.EndDate == null || s.EndDate > now)) ||
                 (s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now))), ct);
    }

    public async Task<bool> CompanyHasActiveSubscriptionAsync(Guid companyId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _db.CompanySubscriptions
            .AnyAsync(s => s.CompanyId == companyId &&
                ((s.Status == SubscriptionStatus.Trial && (s.EndDate == null || s.EndDate > now)) ||
                 (s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now))), ct);
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
            subscription.NextPrice,
            subscription.NextPriceEffectiveDate,
            subscription.BillingPeriod,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status,
            subscription.CancelAtPeriodEnd,
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
            subscription.NextPrice,
            subscription.NextPriceEffectiveDate,
            subscription.BillingPeriod,
            subscription.MaxUsers,
            subscription.StartDate,
            subscription.EndDate,
            subscription.Status,
            subscription.CancelAtPeriodEnd,
            subscription.TrialEndDate
        );
    }

    public async Task CancelUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct)
            ?? throw new InvalidOperationException("No active or trial subscription found.");

        var now = DateTime.UtcNow;

        if (subscription.StripeSubscriptionId is null)
        {
            // No Stripe subscription to schedule a period-end cancel against (legacy data):
            // revoke immediately, otherwise nothing would ever flip this row to Canceled.
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.EndDate = now;
        }
        else if (subscription.Status == SubscriptionStatus.Trial)
        {
            // Keep the trial running until it ends — only disable auto-renewal
            // (cancel_at_period_end). The customer.deleted webhook flips this to Canceled.
            subscription.CancelAtPeriodEnd = true;

            await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }
        else
        {
            // Keep access until the end of the paid period; Stripe stops the renewal
            // (cancel_at_period_end). The customer.deleted webhook flips this to Canceled.
            subscription.CancelAtPeriodEnd = true;

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

        var now = DateTime.UtcNow;

        if (subscription.StripeSubscriptionId is null)
        {
            // No Stripe subscription to schedule a period-end cancel against (legacy data):
            // revoke immediately, otherwise nothing would ever flip this row to Canceled.
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.EndDate = now;
        }
        else if (subscription.Status == SubscriptionStatus.Trial)
        {
            // Keep the trial running until it ends — only disable auto-renewal
            // (cancel_at_period_end). The customer.deleted webhook flips this to Canceled.
            subscription.CancelAtPeriodEnd = true;

            await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }
        else
        {
            // Keep access until the end of the paid period; Stripe stops the renewal
            // (cancel_at_period_end). The customer.deleted webhook flips this to Canceled.
            subscription.CancelAtPeriodEnd = true;

            await _paymentService.CancelSubscriptionAtPeriodEndAsync(subscription.StripeSubscriptionId, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct)
            ?? throw new InvalidOperationException("No active subscription found.");

        if (!subscription.CancelAtPeriodEnd)
            throw new InvalidOperationException("This subscription is not scheduled to cancel.");

        // Removes cancel_at_period_end only — the subscription simply continues renewing.
        // No new invoice is created and the customer is not charged again.
        if (subscription.StripeSubscriptionId is not null)
            await _paymentService.ReactivateSubscriptionAsync(subscription.StripeSubscriptionId, ct);

        subscription.CancelAtPeriodEnd = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateCompanySubscriptionAsync(Guid companyId, Guid actorId, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != actorId)
            throw new UnauthorizedAccessException("Only the company owner can manage subscriptions.");

        var subscription = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial), ct)
            ?? throw new InvalidOperationException("No active subscription found.");

        if (!subscription.CancelAtPeriodEnd)
            throw new InvalidOperationException("This subscription is not scheduled to cancel.");

        // Removes cancel_at_period_end only — the subscription simply continues renewing.
        // No new invoice is created and the customer is not charged again.
        if (subscription.StripeSubscriptionId is not null)
            await _paymentService.ReactivateSubscriptionAsync(subscription.StripeSubscriptionId, ct);

        subscription.CancelAtPeriodEnd = false;
        await _db.SaveChangesAsync(ct);
    }

    private static (SubscriptionStatus Status, DateTime EndDate, DateTime? TrialEndDate) ResolveInitialSubscriptionState(
        BillingPeriod billingPeriod,
        int trialDays,
        DateTime now)
    {
        if (trialDays <= 0)
        {
            var endDate = billingPeriod == BillingPeriod.Monthly
                ? now.AddMonths(1)
                : now.AddYears(1);
            return (SubscriptionStatus.Active, endDate, null);
        }

        var trialEndDate = now.AddDays(trialDays);
        return (SubscriptionStatus.Trial, trialEndDate, trialEndDate);
    }

    private static int CalculateTrialDays(DateTime? trialEndDate, int defaultDays)
    {
        if (trialEndDate is null)
            return defaultDays;

        var now = DateTime.UtcNow;

        if (trialEndDate <= now)
            return 0;

        var remaining = (int)(trialEndDate.Value - now).TotalDays;
        return Math.Max(1, remaining);
    }

    /// <summary>
    /// Most restrictive trial length from the plan's configured days and durable card/email
    /// fingerprints. A null fingerprint TrialEndDate means the trial was already consumed/ended.
    /// </summary>
    private async Task<int> ResolveTrialDaysAsync(
        Guid userId,
        string? email,
        int planTrialDays,
        DateTime? subscriptionTrialEndDate,
        CancellationToken ct)
    {
        if (planTrialDays <= 0)
            return 0;

        var fromSubscription = CalculateTrialDays(subscriptionTrialEndDate, planTrialDays);

        var emailHash = email is not null ? HashEmail(email) : null;
        var fingerprintEnds = await _db.CardFingerprints
            .AsNoTracking()
            .Where(f => f.UserId == userId || (emailHash != null && f.EmailHash == emailHash))
            .Select(f => f.TrialEndDate)
            .ToListAsync(ct);

        if (fingerprintEnds.Count == 0)
            return fromSubscription;

        // Explicit null = trial already ended/consumed for this person
        if (fingerprintEnds.Any(d => d is null))
            return 0;

        var earliest = fingerprintEnds.Min();
        return Math.Min(fromSubscription, CalculateTrialDays(earliest, planTrialDays));
    }

    private static string HashEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(bytes);
    }

    private async Task MarkTrialFingerprintsConsumedAsync(Guid userId, string? email, CancellationToken ct)
    {
        var emailHash = email is not null ? HashEmail(email) : null;
        var records = await _db.CardFingerprints
            .Where(f => f.UserId == userId || (emailHash != null && f.EmailHash == emailHash))
            .ToListAsync(ct);

        foreach (var record in records)
            record.TrialEndDate = null;
    }

    private async Task HandleCheckoutCompletedAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (!evt.Metadata.TryGetValue("userId", out var userIdRaw) ||
            !Guid.TryParse(userIdRaw, out var userId))
            throw new InvalidOperationException($"Missing or invalid 'userId' in {evt.Type} metadata.");

        if (!evt.Metadata.TryGetValue("planId", out var planIdRaw) ||
            !Guid.TryParse(planIdRaw, out var planId))
            throw new InvalidOperationException($"Missing or invalid 'planId' in {evt.Type} metadata.");

        if (!evt.Metadata.TryGetValue("billingPeriod", out var billingRaw) ||
            !Enum.TryParse<BillingPeriod>(billingRaw, out var billingPeriod))
            throw new InvalidOperationException($"Missing or invalid 'billingPeriod' in {evt.Type} metadata.");

        var isCompany = evt.Metadata.TryGetValue("isCompany", out var isCompanyRaw) &&
            bool.TryParse(isCompanyRaw, out var isCompanyParsed) && isCompanyParsed;

        var trialDays = evt.Metadata.TryGetValue("trialDays", out var trialRaw) &&
            int.TryParse(trialRaw, out var trialDaysParsed) ? trialDaysParsed : DefaultTrialDays;

        var now = DateTime.UtcNow;

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is not null && evt.StripeCustomerId is not null)
        {
            user.StripeCustomerId = evt.StripeCustomerId;
        }

        var stripeSubscriptionId = evt.StripeSubscriptionId
            ?? throw new InvalidOperationException($"No subscription ID in {evt.Type} event for customer {evt.StripeCustomerId}");

        if (await _db.UserSubscriptions.AnyAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct) ||
            await _db.CompanySubscriptions.AnyAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct))
            return;

        UserSubscription? userSubRef = null;
        CompanySubscription? companySubRef = null;

        if (isCompany)
        {
            if (!evt.Metadata.TryGetValue("companyId", out var companyIdRaw) ||
                !Guid.TryParse(companyIdRaw, out var companyId))
                throw new InvalidOperationException($"Missing or invalid 'companyId' in {evt.Type} metadata.");
            var existing = await _db.CompanySubscriptions
                .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

            var (status, endDate, trialEndDate) = ResolveInitialSubscriptionState(billingPeriod, trialDays, now);

            if (existing is not null)
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                existing.PlanId = planId;
                existing.Price = plan is not null
                    ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                    : 0;
                existing.BillingPeriod = billingPeriod;
                existing.MaxUsers = plan?.MaxUsers;
                existing.StartDate = now;
                existing.EndDate = endDate;
                existing.Status = status;
                existing.CancelAtPeriodEnd = false;
                existing.StripeSubscriptionId = stripeSubscriptionId;
                existing.TrialEndDate = trialEndDate ?? existing.TrialEndDate;
                companySubRef = existing;
            }
            else
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                companySubRef = new CompanySubscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    PlanId = planId,
                    Price = plan is not null
                        ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                        : 0,
                    BillingPeriod = billingPeriod,
                    MaxUsers = plan?.MaxUsers,
                    StartDate = now,
                    EndDate = endDate,
                    Status = status,
                    StripeSubscriptionId = stripeSubscriptionId,
                    TrialEndDate = trialEndDate
                };
                _db.CompanySubscriptions.Add(companySubRef);
            }
        }
        else
        {
            var existing = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId, ct);

            var (status, endDate, trialEndDate) = ResolveInitialSubscriptionState(billingPeriod, trialDays, now);

            if (existing is not null)
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                existing.PlanId = planId;
                existing.Price = plan is not null
                    ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                    : 0;
                existing.BillingPeriod = billingPeriod;
                existing.StartDate = now;
                existing.EndDate = endDate;
                existing.Status = status;
                existing.CancelAtPeriodEnd = false;
                existing.StripeSubscriptionId = stripeSubscriptionId;
                existing.TrialEndDate = trialEndDate ?? existing.TrialEndDate;
                userSubRef = existing;
            }
            else
            {
                var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
                userSubRef = new UserSubscription
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlanId = planId,
                    Price = plan is not null
                        ? (billingPeriod == BillingPeriod.Monthly ? plan.MonthlyPrice : plan.YearlyPrice)
                        : 0,
                    BillingPeriod = billingPeriod,
                    StartDate = now,
                    EndDate = endDate,
                    Status = status,
                    StripeSubscriptionId = stripeSubscriptionId,
                    TrialEndDate = trialEndDate
                };
                _db.UserSubscriptions.Add(userSubRef);
            }
        }

        if (user is not null)
        {
            var plan = await _db.SubscriptionPlans.FindAsync([planId], ct);
            if (plan is not null && user.UserType != plan.UserType)
                user.UserType = plan.UserType;
        }

        // Track card fingerprint to prevent trial abuse across accounts.
        // Match existing rows BEFORE insert, correct the subscription, then persist
        // only the corrected TrialEndDate (never store the pre-correction full trial).
        // No trial = nothing to abuse, so fingerprint tracking is skipped entirely.
        if (trialDays > 0 && evt.StripeSubscriptionId is not null)
        {
            var fingerprint = await _paymentService.GetPaymentMethodFingerprintBySubscriptionAsync(evt.StripeSubscriptionId, ct);

            if (fingerprint is not null)
            {
                var userEmailHash = user?.Email is not null ? HashEmail(user.Email) : null;
                var matchingFingerprints = await _db.CardFingerprints
                    .Where(f => f.Fingerprint == fingerprint)
                    .ToListAsync(ct);

                var samePersonRecords = matchingFingerprints
                    .Where(f => f.UserId == userId
                        || (userEmailHash is not null && f.UserId == null && f.EmailHash == userEmailHash))
                    .ToList();

                DateTime? correctedTrialEndDate = userSubRef?.TrialEndDate ?? companySubRef?.TrialEndDate;

                if (matchingFingerprints.Count > 0)
                {
                    var datedEnds = samePersonRecords
                        .Where(f => f.TrialEndDate is not null)
                        .Select(f => f.TrialEndDate!.Value)
                        .ToList();
                    var earliestSamePersonEnd = datedEnds.Count > 0 ? datedEnds.Min() : (DateTime?)null;
                    var trialAlreadyConsumed = samePersonRecords.Any(f => f.TrialEndDate is null);
                    var canResume = samePersonRecords.Count > 0
                        && !trialAlreadyConsumed
                        && earliestSamePersonEnd is not null
                        && earliestSamePersonEnd > DateTime.UtcNow;

                    if (canResume)
                    {
                        // Same person returning with active trial — resume earliest remaining end
                        await _paymentService.UpdateSubscriptionTrialEndAsync(
                            stripeSubscriptionId, earliestSamePersonEnd.Value, ct);

                        correctedTrialEndDate = earliestSamePersonEnd;

                        if (userSubRef is not null)
                        {
                            userSubRef.TrialEndDate = earliestSamePersonEnd;
                            userSubRef.EndDate = earliestSamePersonEnd;
                        }
                        else if (companySubRef is not null)
                        {
                            companySubRef.TrialEndDate = earliestSamePersonEnd;
                            companySubRef.EndDate = earliestSamePersonEnd;
                        }
                    }
                    else
                    {
                        // Different person, consumed trial, or expired trial — end immediately
                        await _paymentService.EndTrialImmediatelyAsync(evt.StripeSubscriptionId, ct);

                        correctedTrialEndDate = null;

                        var renewalDate = DateTime.UtcNow.AddMonths(1);
                        if (billingPeriod == BillingPeriod.Yearly)
                            renewalDate = DateTime.UtcNow.AddYears(1);

                        if (companySubRef is not null)
                        {
                            companySubRef.Status = SubscriptionStatus.Active;
                            companySubRef.EndDate = renewalDate;
                            companySubRef.TrialEndDate = null;
                        }
                        else if (userSubRef is not null)
                        {
                            userSubRef.Status = SubscriptionStatus.Active;
                            userSubRef.EndDate = renewalDate;
                            userSubRef.TrialEndDate = null;
                        }
                    }

                    // Keep all same-person fingerprint evidence consistent (no poisoned full dates)
                    foreach (var record in samePersonRecords)
                        record.TrialEndDate = correctedTrialEndDate;
                }

                var existingForUser = matchingFingerprints.Find(f => f.UserId == userId);
                if (existingForUser is not null)
                {
                    existingForUser.TrialEndDate = correctedTrialEndDate;
                    existingForUser.EmailHash = userEmailHash ?? existingForUser.EmailHash;
                    existingForUser.StripePaymentMethodId = evt.StripeSubscriptionId;
                }
                else
                {
                    _db.CardFingerprints.Add(new CardFingerprint
                    {
                        Id = Guid.NewGuid(),
                        Fingerprint = fingerprint,
                        UserId = userId,
                        EmailHash = userEmailHash,
                        TrialEndDate = correctedTrialEndDate,
                        StripePaymentMethodId = evt.StripeSubscriptionId
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoicePaidAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.StripeSubscriptionId is null)
            return;

        var now = DateTime.UtcNow;

        var userSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (userSub is not null)
        {
            var wasTrial = userSub.Status == SubscriptionStatus.Trial;
            if (wasTrial)
            {
                userSub.Status = SubscriptionStatus.Active;
                userSub.TrialEndDate = null;
            }

            userSub.EndDate = userSub.BillingPeriod == BillingPeriod.Monthly
                ? now.AddMonths(1)
                : now.AddYears(1);

            ApplyPendingPriceChange(userSub);

            if (wasTrial)
            {
                var email = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userSub.UserId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
                await MarkTrialFingerprintsConsumedAsync(userSub.UserId, email, ct);
            }

            await _db.SaveChangesAsync(ct);
            return;
        }

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (companySub is not null)
        {
            var wasTrial = companySub.Status == SubscriptionStatus.Trial;
            if (wasTrial)
            {
                companySub.Status = SubscriptionStatus.Active;
                companySub.TrialEndDate = null;
            }

            companySub.EndDate = companySub.BillingPeriod == BillingPeriod.Monthly
                ? now.AddMonths(1)
                : now.AddYears(1);

            ApplyPendingPriceChange(companySub);

            if (wasTrial)
            {
                var owner = await _db.Companies.AsNoTracking()
                    .Where(c => c.Id == companySub.CompanyId)
                    .Select(c => new { c.OwnerId })
                    .FirstOrDefaultAsync(ct);

                if (owner is not null)
                {
                    var email = await _db.Users.AsNoTracking()
                        .Where(u => u.Id == owner.OwnerId)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync(ct);
                    await MarkTrialFingerprintsConsumedAsync(owner.OwnerId, email, ct);
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task HandleSubscriptionUpdatedAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.StripeSubscriptionId is null)
            return;

        // A trial that ended successfully flips to "active" in Stripe. Fall back to this
        // event when the invoice.paid webhook is delayed or not delivered at all.
        if (evt.StripeStatus != "active")
            return;

        var now = DateTime.UtcNow;

        var userSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (userSub is not null)
        {
            if (userSub.Status == SubscriptionStatus.Trial)
            {
                userSub.Status = SubscriptionStatus.Active;
                userSub.TrialEndDate = null;
                userSub.EndDate = userSub.BillingPeriod == BillingPeriod.Monthly
                    ? now.AddMonths(1)
                    : now.AddYears(1);

                var email = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userSub.UserId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
                await MarkTrialFingerprintsConsumedAsync(userSub.UserId, email, ct);
            }

            await _db.SaveChangesAsync(ct);
            return;
        }

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (companySub is not null && companySub.Status == SubscriptionStatus.Trial)
        {
            companySub.Status = SubscriptionStatus.Active;
            companySub.TrialEndDate = null;
            companySub.EndDate = companySub.BillingPeriod == BillingPeriod.Monthly
                ? now.AddMonths(1)
                : now.AddYears(1);

            var owner = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == companySub.CompanyId)
                .Select(c => new { c.OwnerId })
                .FirstOrDefaultAsync(ct);

            if (owner is not null)
            {
                var email = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == owner.OwnerId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
                await MarkTrialFingerprintsConsumedAsync(owner.OwnerId, email, ct);
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private static void ApplyPendingPriceChange(UserSubscription subscription)
    {
        if (subscription.NextPrice is not null)
        {
            subscription.Price = subscription.NextPrice.Value;
            subscription.NextPrice = null;
            subscription.NextPriceEffectiveDate = null;
        }
    }

    private static void ApplyPendingPriceChange(CompanySubscription subscription)
    {
        if (subscription.NextPrice is not null)
        {
            subscription.Price = subscription.NextPrice.Value;
            subscription.NextPrice = null;
            subscription.NextPriceEffectiveDate = null;
        }
    }

    private async Task HandleSubscriptionDeletedAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.StripeSubscriptionId is null)
            return;

        var now = DateTime.UtcNow;

        var userSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (userSub is not null)
        {
            userSub.Status = SubscriptionStatus.Canceled;
            userSub.EndDate = now;
            userSub.CancelAtPeriodEnd = false;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (companySub is not null)
        {
            companySub.Status = SubscriptionStatus.Canceled;
            companySub.EndDate = now;
            companySub.CancelAtPeriodEnd = false;
            await _db.SaveChangesAsync(ct);
        }
    }
}
